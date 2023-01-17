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

        Type fieldType;

        Type declaringType;

        bool isStatic;

        public DynBridgeFieldAccessor(FieldInfo fieldInfo)
        {
            var flag = DynamicBridge.IL2CPPBridge.Flag.DB_BOXED_STRUCT_INSTANCE | DynamicBridge.IL2CPPBridge.Flag.DB_KEEPING_IL2CPP_STRING;

            fieldName = fieldInfo.Name;
            fieldType = fieldInfo.FieldType;
            if (fieldType.IsValueType && !fieldType.IsPrimitive && !fieldType.IsEnum)
            {
                flag |= DynamicBridge.IL2CPPBridge.Flag.DB_BOXED_FIELD_ACCESSOR;
            }
            declaringType = fieldInfo.DeclaringType;
            isStatic = fieldInfo.IsStatic;

            DynamicBridge.IL2CPPBridge.GetField(fieldInfo, out getter, out setter, flag);
        }

        public unsafe object GetValue(object obj)
        {
            long val;
            if (isStatic)
            {
                DynamicBridge.Extras extras = new DynamicBridge.Extras();
                val = DynamicBridge.Bridge.InvokeMethodUnchecked(ref getter, 0, 0, &extras);
                if (extras.errorCode != 0)
                {
                    throw new TargetException($"can not access field [{declaringType}.{fieldName}], error code: {extras.errorCode}");
                }
            }
            else
            {
                IntPtr ptr = DynamicBridge.IL2CPPBridge.ObjectToPointer(obj);
                if (ptr == IntPtr.Zero)
                {
                    throw new NullReferenceException(declaringType + "." + fieldName);
                }
                DynamicBridge.Extras extras = new DynamicBridge.Extras();
                val = DynamicBridge.Bridge.InvokeMethodUnchecked(ref getter, (long)ptr, 0, &extras);
                if (extras.errorCode != 0)
                {
                    throw new TargetException($"can not access field [{declaringType}.{fieldName}], error code: {extras.errorCode}");
                }
            }
            if (fieldType.IsPrimitive || fieldType.IsEnum)
            {
                return Convert.ChangeType(val, fieldType);
            }
            else
            {
                return DynamicBridge.IL2CPPBridge.PointerToObject((IntPtr)val);
            }
        }

        public unsafe void SetValue(object obj, object value)
        {
            long val;
            if (fieldType.IsPrimitive || fieldType.IsEnum)
            {
                val = Convert.ToInt64(value);
            }
            else
            {
                val = (long)DynamicBridge.IL2CPPBridge.ObjectToPointer(value);
            }
            if (isStatic)
            {
                DynamicBridge.Extras extras = new DynamicBridge.Extras();
                DynamicBridge.Bridge.InvokeMethodUnchecked(ref setter, val, 0, &extras);
                if (extras.errorCode != 0)
                {
                    throw new TargetException($"can not access field [{declaringType}.{fieldName}], error code: {extras.errorCode}");
                }
            }
            else
            {
                IntPtr ptr = DynamicBridge.IL2CPPBridge.ObjectToPointer(obj);
                if (ptr == IntPtr.Zero)
                {
                    throw new NullReferenceException(declaringType + "." + fieldName);
                }
                DynamicBridge.Extras extras = new DynamicBridge.Extras();
                DynamicBridge.Bridge.InvokeMethodUnchecked(ref setter, (long)ptr, val, &extras);
                if (extras.errorCode != 0)
                {
                    throw new TargetException($"can not access field [{declaringType}.{fieldName}], error code: {extras.errorCode}");
                }
            }
        }

        public unsafe void Load(VirtualMachine virtualMachine, Value* evaluationStackBase, Value* evaluationStackPointer, object[] managedStack)
        {
            long val;
            if (isStatic)
            {
                DynamicBridge.Extras extras = new DynamicBridge.Extras();
                val = DynamicBridge.Bridge.InvokeMethodUnchecked(ref getter, 0, 0, &extras);
                if (extras.errorCode != 0)
                {
                    throw new TargetException($"can not access field [{declaringType}.{fieldName}], error code: {extras.errorCode}");
                }
            }
            else
            {
                DynamicBridge.Extras extras = new DynamicBridge.Extras();
                object obj = EvaluationStackOperation.ToObject(evaluationStackBase, evaluationStackPointer, managedStack, declaringType, virtualMachine, false);
                IntPtr ptr = DynamicBridge.IL2CPPBridge.ObjectToPointer(obj);
                if (ptr == IntPtr.Zero)
                {
                    throw new NullReferenceException(declaringType + "." + fieldName);
                }
                val = DynamicBridge.Bridge.InvokeMethodUnchecked(ref getter, (long)ptr, 0, &extras);
                if (extras.errorCode != 0)
                {
                    throw new TargetException($"can not access field [{declaringType}.{fieldName}], error code: {extras.errorCode}");
                }
            }
            EvaluationStackOperation.PushValue(evaluationStackBase, evaluationStackPointer, managedStack, (DynamicBridge.Type)getter.returnType, &val);
        }

        public unsafe void Store(VirtualMachine virtualMachine, Value* evaluationStackBase, Value* evaluationStackPointer, object[] managedStack)
        {
            long val;
            EvaluationStackOperation.ToValue(evaluationStackBase, evaluationStackPointer, managedStack, (DynamicBridge.Type)getter.returnType, &val, virtualMachine);
            if (isStatic)
            {
                DynamicBridge.Extras extras = new DynamicBridge.Extras();
                DynamicBridge.Bridge.InvokeMethodUnchecked(ref setter, val, 0, &extras);
                if (extras.errorCode != 0)
                {
                    throw new TargetException($"can not access field [{declaringType}.{fieldName}], error code: {extras.errorCode}");
                }
            }
            else
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
        }
    }
}
#endif