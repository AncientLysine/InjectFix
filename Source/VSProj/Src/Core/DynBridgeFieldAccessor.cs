using System;
using System.Reflection;

#if ENABLE_IL2CPP
namespace IFix.Core
{
    internal class DynBridgeFieldAccessor
    {
        DynamicBridge.Method getter;

        DynamicBridge.Method setter;

        string fieldName;

        Type declaringType;

        bool isStatic;

        public DynBridgeFieldAccessor(FieldInfo fieldInfo)
        {
            var flag = DynamicBridge.IL2CPPBridge.Flag.DB_BOXED_STRUCT_INSTANCE | DynamicBridge.IL2CPPBridge.Flag.DB_KEEPING_IL2CPP_STRING;

            fieldName = fieldInfo.Name;
            declaringType = fieldInfo.DeclaringType;
            isStatic = fieldInfo.IsStatic;
            Type fieldType = fieldInfo.FieldType;
            if (fieldType.IsValueType && !fieldType.IsPrimitive)
            {
                flag |= DynamicBridge.IL2CPPBridge.Flag.DB_BOXED_FIELD_ACCESSOR;
            }

            DynamicBridge.IL2CPPBridge.GetField(fieldInfo, out getter, out setter, flag);
        }

        public unsafe void Load(VirtualMachine virtualMachine, Value* evaluationStackBase, Value* evaluationStackPointer, object[] managedStack)
        {
            long val;
            if (!isStatic)
            {
                object obj = EvaluationStackOperation.ToObject(evaluationStackBase, evaluationStackPointer, managedStack, declaringType, virtualMachine, false);
                IntPtr ptr = DynamicBridge.IL2CPPBridge.ObjectToPointer(obj);
                if (ptr == IntPtr.Zero)
                {
                    throw new NullReferenceException(declaringType + "." + fieldName);
                }

                val = DynamicBridge.Bridge.InvokeMethod<IntPtr, long>(ref getter, ptr);
            }
            else
            {
                val = DynamicBridge.Bridge.InvokeMethod<long>(ref getter);
            }
            EvaluationStackOperation.PushValue(evaluationStackBase, evaluationStackPointer, managedStack, (DynamicBridge.Type)getter.returnType, &val);
        }

        public unsafe void Store(VirtualMachine virtualMachine, Value* evaluationStackBase, Value* evaluationStackPointer, object[] managedStack)
        {
            long val;
            EvaluationStackOperation.ToValue(evaluationStackBase, evaluationStackPointer, managedStack, (DynamicBridge.Type)getter.returnType, &val, virtualMachine);
            if (!isStatic)
            {
                Value* ins = evaluationStackPointer - 1;
                object obj = EvaluationStackOperation.ToObject(evaluationStackBase, ins, managedStack, declaringType, virtualMachine, false);
                IntPtr ptr = DynamicBridge.IL2CPPBridge.ObjectToPointer(obj);
                if (ptr == IntPtr.Zero)
                {
                    throw new NullReferenceException($"{declaringType}.{fieldName}");
                }

                DynamicBridge.Extras extras = new DynamicBridge.Extras();
                DynamicBridge.Bridge.InvokeMethodUnchecked(ref setter, (long)ptr, val, &extras);
                if (extras.errorCode != 0)
                {
                    throw new TargetException($"can not access field [{declaringType}.{fieldName}], error code: {extras.errorCode}");
                }

                //如果field，array元素是值类型，需要重新update回去
                if ((ins->Type == ValueType.FieldReference
                    || ins->Type == ValueType.ChainFieldReference
                    || ins->Type == ValueType.StaticFieldReference
                    || ins->Type == ValueType.ArrayReference)
                    && declaringType.IsValueType)
                {
                    EvaluationStackOperation.UpdateReference(evaluationStackBase, ins, managedStack, obj, virtualMachine, declaringType);
                }
            }
            else
            {
                DynamicBridge.Bridge.InvokeMethod(ref setter, val);
            }
        }
    }
}
#endif