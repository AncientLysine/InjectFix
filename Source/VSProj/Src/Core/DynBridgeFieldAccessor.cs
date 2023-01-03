using System;
using System.Reflection;

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
            DynamicBridge.IL2CPPBridge.GetField(fieldInfo, out getter, out setter, DynamicBridge.IL2CPPBridge.Flag.DB_BOXED_STRUCT_INSTANCE | DynamicBridge.IL2CPPBridge.Flag.DB_KEEPING_IL2CPP_STRING);
            fieldName = fieldInfo.Name;
            declaringType = fieldInfo.DeclaringType;
            isStatic = fieldInfo.IsStatic;
        }

        public unsafe void Load(VirtualMachine vm, Value* evaluationStackBase, Value* evaluationStackPointer, object[] managedStack)
        {
            long val;
            if (!isStatic)
            {
                object obj = EvaluationStackOperation.ToObject(evaluationStackBase, evaluationStackPointer, managedStack, declaringType, vm, false);
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

        public unsafe void Store(VirtualMachine vm, Value* evaluationStackBase, Value* evaluationStackPointer, object[] managedStack)
        {
            long val;
            EvaluationStackOperation.ToValue(evaluationStackBase, evaluationStackPointer, managedStack, (DynamicBridge.Type)getter.returnType, &val);
            if (!isStatic)
            {
                Value* ins = evaluationStackPointer - 1;
                object obj = EvaluationStackOperation.ToObject(evaluationStackBase, ins, managedStack, declaringType, vm, false);
                IntPtr ptr = DynamicBridge.IL2CPPBridge.ObjectToPointer(obj);
                if (ptr == IntPtr.Zero)
                {
                    throw new NullReferenceException(declaringType + "." + fieldName);
                }

                DynamicBridge.Bridge.InvokeMethod(ref setter, ptr, val);

                //如果field，array元素是值类型，需要重新update回去
                if ((ins->Type == ValueType.FieldReference
                    || ins->Type == ValueType.ChainFieldReference
                    || ins->Type == ValueType.StaticFieldReference
                    || ins->Type == ValueType.ArrayReference)
                    && declaringType.IsValueType)
                {
                    EvaluationStackOperation.UpdateReference(evaluationStackBase, ins, managedStack, obj, vm, declaringType);
                }
            }
            else
            {
                DynamicBridge.Bridge.InvokeMethod(ref setter, val);
            }
        }
    }
}