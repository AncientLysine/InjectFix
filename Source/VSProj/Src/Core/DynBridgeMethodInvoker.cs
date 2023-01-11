/*
 * Tencent is pleased to support the open source community by making InjectFix available.
 * Copyright (C) 2019 THL A29 Limited, a Tencent company.  All rights reserved.
 * InjectFix is licensed under the MIT License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
 * This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
 */

using System;
using System.Reflection;
using System.Runtime.Remoting.Activation;

#if ENABLE_IL2CPP
namespace IFix.Core
{
    internal class DynBridgeMethodInvoker
    {
        DynamicBridge.Method method;

        string methodName;

        Type declaringType;

        int paramCount;

        bool hasThis;

        bool hasReturn;

        bool[] refFlags;

        bool[] outFlags;

        bool isNullableHasValue = false;
        bool isNullableValue = false;

        public DynBridgeMethodInvoker(MethodBase method)
        {
            var flag = DynamicBridge.IL2CPPBridge.Flag.DB_BOXED_STRUCT_INSTANCE | DynamicBridge.IL2CPPBridge.Flag.DB_KEEPING_IL2CPP_STRING;
            
            methodName = method.Name;
            declaringType = method.DeclaringType;
            var paramerInfos = method.GetParameters();
            paramCount = paramerInfos.Length;
            refFlags = new bool[paramCount];
            outFlags = new bool[paramCount];
            //args = new object[paramCount];

            for (int i = 0; i < paramerInfos.Length; i++)
            {
                var paramInfo = paramerInfos[i];
                outFlags[i] = !paramInfo.IsIn && paramInfo.IsOut;
                if (paramInfo.ParameterType.IsByRef)
                {
                    refFlags[i] = true;
                }
                else
                {
                    refFlags[i] = false;
                }
                if (paramInfo.ParameterType.IsValueType && !paramInfo.ParameterType.IsPrimitive)
                {
                    flag |= DynamicBridge.IL2CPPBridge.Flag.DB_USING_IL2CPP_RUNTIME_INVOKER;
                }
            }
            Type returnType;
            if (method.IsConstructor)
            {
                returnType = (method as ConstructorInfo).DeclaringType;
                hasReturn = true;
            }
            else
            {
                returnType = (method as MethodInfo).ReturnType;
                hasReturn = returnType != typeof(void);
            }
            if (!hasReturn || returnType.IsClass)
            {
                flag |= DynamicBridge.IL2CPPBridge.Flag.DB_USING_IL2CPP_RUNTIME_INVOKER;
            }
            hasThis = !method.IsStatic;
            bool isNullableMethod = method.DeclaringType.IsGenericType
                && method.DeclaringType.GetGenericTypeDefinition() == typeof(Nullable<>);
            isNullableHasValue = isNullableMethod && method.Name == "get_HasValue";
            isNullableValue = isNullableMethod && method.Name == "get_Value";

            DynamicBridge.IL2CPPBridge.GetMethod(method, out this.method, flag);
        }

        public unsafe void Invoke(VirtualMachine virtualMachine, ref Call call, bool isInstantiate)
        {
            var pushResult = false;
            try
            {
                long p0 = 0, p1 = 0;
                long* buffer = stackalloc long[8];
                DynamicBridge.Extras* extras = (DynamicBridge.Extras*)buffer;
                extras->errorCode = 0;
                extras->capacity = (uint)(sizeof(long) * 8 - sizeof(DynamicBridge.Extras) + DynamicBridge.Extras.DEF_STACK_CAP);
                extras->position = 0;

                object newObj = null;
                if (isInstantiate)
                {
                    newObj = Activator.CreateInstance(declaringType);
                }

                int outStart = 0;
                if (hasThis)
                {
                    outStart = 1;
                }
                int argStart = 0;
                if (isInstantiate)
                {
                    argStart = 1;
                }
                int count = method.paramCount;
                if (count >= 1)
                { 
                    if (isInstantiate)
                    {
                        *(IntPtr*)&p0 = DynamicBridge.IL2CPPBridge.ObjectToPointer(newObj);
                    }
                    else if (hasThis || !outFlags[0])
                    {
                        DynamicBridge.Type paramType = (DynamicBridge.Type)method.paramType[0];
                        int argIndex = 0;
                        EvaluationStackOperation.ToValue(call.evaluationStackBase, &call.argumentBase[argIndex], call.managedStack, paramType, &p0, virtualMachine, !hasThis);
                    }
                }
                if (count >= 2)
                { 
                    if (!outFlags[1 - outStart])
                    {
                        DynamicBridge.Type paramType = (DynamicBridge.Type)method.paramType[1];
                        int argIndex = 1 - argStart;
                        EvaluationStackOperation.ToValue(call.evaluationStackBase, &call.argumentBase[argIndex], call.managedStack, paramType, &p1, virtualMachine);
                    }
                }
                for (int reverse = count - 1; reverse >= 2; --reverse)
                {
                    if (!outFlags[reverse - outStart])
                    {
                        //args[i] = EvaluationStackOperation.ToObject(call.evaluationStackBase, pArg, managedStack,
                        //    rawTypes[i], virtualMachine);
                        DynamicBridge.Type paramType = (DynamicBridge.Type)method.paramType[reverse];
                        int paramSize = DynamicBridge.Bridge.SizeOfType(paramType);
                        if (paramSize < 0)
                        {
                            break;
                        }
                        int argIndex = reverse - argStart;
                        long pi = 0;
                        EvaluationStackOperation.ToValue(call.evaluationStackBase, &call.argumentBase[argIndex], call.managedStack, paramType, &pi, virtualMachine);
#if NET_4_6
                        Buffer.MemoryCopy(&pi, &extras->stack[extras->position], extras->capacity - extras->position, paramSize);
#else
                        if (extras->position + paramSize > extras->capacity)
                        {
                            throw new TargetException($"can not invoke method [{declaringType}.{methodName}], stack overflow");
                        }
                        byte* dst = &extras->stack[extras->position];
                        byte* src = (byte*)&pi;
                        for (int idx = 0; idx < paramSize; ++idx)
                        {
                            dst[idx] = src[idx];
                        }
#endif
                        extras->position += (uint)paramSize;
                    }
                }
                if (hasThis && p0 == 0)
                {
                    throw new TargetException($"can not invoke method [{declaringType}.{methodName}], Non-static method require instance but got null.");
                }
                long rv = DynamicBridge.Bridge.InvokeMethodUnchecked(ref method, p0, p1, extras);
                if (extras->errorCode != 0)
                {
                    throw new TargetException($"can not invoke method [{declaringType}.{methodName}], error code: {extras->errorCode}");
                }
                if (isInstantiate)
                {
                    call.PushObjectAsResult(newObj, declaringType);
                    pushResult = true;
                }
                else if ((DynamicBridge.Type)method.returnType != DynamicBridge.Type.DB_VOID)
                {
                    //call.PushObjectAsResult(rv, returnType);
                    EvaluationStackOperation.PushValue(call.evaluationStackBase, call.argumentBase, call.managedStack, (DynamicBridge.Type)method.returnType, &rv);
                    call.currentTop = call.argumentBase + 1;
                    pushResult = true;
                }
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
            finally
            {
                Value* pArg = call.argumentBase;
                if (pushResult)
                {
                    pArg++;
                }
                for (int i = (pushResult ? 1 : 0); i < paramCount + ((hasThis && !isInstantiate) ? 1 : 0); i++)
                {
                    call.managedStack[pArg - call.evaluationStackBase] = null;
                    pArg++;
                }
            }
        }
    }
}
#endif