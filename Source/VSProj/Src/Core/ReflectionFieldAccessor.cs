using System;
using System.Reflection;

namespace IFix.Core
{
    internal class ReflectionFieldAccessor : ExternAccessor
    {
        FieldInfo fieldInfo;

        string fieldName;

        Type fieldType;

        Type declaringType;

        bool isStatic;

        public ReflectionFieldAccessor(FieldInfo fieldInfo)
        {
            this.fieldInfo = fieldInfo;
            fieldName = fieldInfo.Name;
            fieldType = fieldInfo.FieldType;
            declaringType = fieldInfo.DeclaringType;
            isStatic = fieldInfo.IsStatic;
        }

        public override object GetValue(object obj)
        {
            return fieldInfo.GetValue(obj);
        }

        public override void SetValue(object obj, object val)
        {
            fieldInfo.SetValue(obj, val);
        }

        public override unsafe void Load(VirtualMachine vm, Value* evaluationStackBase, Value* evaluationStackPointer, object[] managedStack)
        {
            object val;
            if (!isStatic)
            {
                object obj = EvaluationStackOperation.ToObject(evaluationStackBase, evaluationStackPointer, managedStack, declaringType, vm, false);
                if (obj == null)
                {
                    throw new NullReferenceException(declaringType + "." + fieldName);
                }

                val = fieldInfo.GetValue(obj);
            }
            else
            {
                val = fieldInfo.GetValue(null);
            }
            EvaluationStackOperation.PushObject(evaluationStackBase, evaluationStackPointer, managedStack, val, fieldType);
        }

        public override unsafe void Store(VirtualMachine vm, Value* evaluationStackBase, Value* evaluationStackPointer, object[] managedStack)
        {
            object val = EvaluationStackOperation.ToObject(evaluationStackBase, evaluationStackPointer, managedStack, fieldType, vm);
            if (!isStatic)
            {
                Value* ins = evaluationStackPointer - 1;
                object obj = EvaluationStackOperation.ToObject(evaluationStackBase, ins, managedStack, declaringType, vm, false);
                if (obj == null)
                {
                    throw new NullReferenceException(declaringType + "." + fieldName);
                }

                fieldInfo.SetValue(obj, val);

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
                fieldInfo.SetValue(null, val);
            }
        }
    }
}