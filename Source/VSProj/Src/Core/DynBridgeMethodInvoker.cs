/*
 * Tencent is pleased to support the open source community by making InjectFix available.
 * Copyright (C) 2019 THL A29 Limited, a Tencent company.  All rights reserved.
 * InjectFix is licensed under the MIT License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
 * This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
 */

using System;
using System.Reflection;

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
            DynamicBridge.IL2CPPBridge.GetMethod(method as MethodInfo, out this.method, DynamicBridge.IL2CPPBridge.Flag.DB_BOXED_STRUCT_INSTANCE | DynamicBridge.IL2CPPBridge.Flag.DB_KEEPING_IL2CPP_STRING);
            methodName = method.Name;
            declaringType = method.DeclaringType;
            var paramerInfos = method.GetParameters();
            paramCount = paramerInfos.Length;
            refFlags = new bool[paramCount];
            outFlags = new bool[paramCount];
            //args = new object[paramCount];

            for (int i = 0; i < paramerInfos.Length; i++)
            {
                outFlags[i] = !paramerInfos[i].IsIn && paramerInfos[i].IsOut;
                if (paramerInfos[i].ParameterType.IsByRef)
                {
                    refFlags[i] = true;
                }
                else
                {
                    refFlags[i] = false;
                }
            }
            if (method.IsConstructor)
            {
                hasReturn = true;
            }
            else
            {
                Type returnType = (method as MethodInfo).ReturnType;
                hasReturn = returnType != typeof(void);
            }
            hasThis = !method.IsStatic;
            bool isNullableMethod = method.DeclaringType.IsGenericType
                && method.DeclaringType.GetGenericTypeDefinition() == typeof(Nullable<>);
            isNullableHasValue = isNullableMethod && method.Name == "get_HasValue";
            isNullableValue = isNullableMethod && method.Name == "get_Value";
        }

        public unsafe void Invoke(VirtualMachine virtualMachine, ref Call call, bool isInstantiate)
        {
            var pushResult = false;
            try
            {
                long p0 = 0, p1 = 0;
                DynamicBridge.Extras extras = new DynamicBridge.Extras(1024);
                int paramStart = 0;
                if (hasThis && !isInstantiate)
                {
                    paramStart = 1;
                }
                int count = paramCount + paramStart;
                if (count > 0)
                {
                    if (paramStart > 0 || !outFlags[0])
                    {
                        DynamicBridge.Type paramType = (DynamicBridge.Type)method.paramType[0];
                        EvaluationStackOperation.ToValue(call.evaluationStackBase, &call.argumentBase[0], call.managedStack, paramType, &p0);
                    }
                }
                if (count > 1)
                {
                    if (!outFlags[1 - paramStart])
                    {
                        DynamicBridge.Type paramType = (DynamicBridge.Type)method.paramType[1];
                        EvaluationStackOperation.ToValue(call.evaluationStackBase, &call.argumentBase[1], call.managedStack, paramType, &p1);
                    }
                }
                for (int i = count - 1; i >= 2; --i)
                {
                    if (!outFlags[i - paramStart])
                    {
                        //args[i] = EvaluationStackOperation.ToObject(call.evaluationStackBase, pArg, managedStack,
                        //    rawTypes[i], virtualMachine);
                        DynamicBridge.Type paramType = (DynamicBridge.Type)method.paramType[i];
                        int paramSize = DynamicBridge.Bridge.SizeOfType(paramType);
                        if (paramSize < 0)
                        {
                            break;
                        }
                        long pi = 0;
                        EvaluationStackOperation.ToValue(call.evaluationStackBase, &call.argumentBase[i], call.managedStack, paramType, &pi);
                        Buffer.MemoryCopy(&pi, &extras.stack[extras.position], extras.capacity - extras.position, paramSize);
                        extras.position += (uint)paramSize;
                    }
                }
                if (hasThis && p0 == 0)
                {
                    throw new TargetException($"can not invoke method [{declaringType}.{methodName}], Non-static method require instance but got null.");
                }
                long rv = DynamicBridge.Bridge.InvokeMethod<long, long, long>(ref method, p0, p1, ref extras);
                if ((DynamicBridge.Type)method.returnType != DynamicBridge.Type.DB_VOID)
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
