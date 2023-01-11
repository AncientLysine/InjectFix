/*
 * Tencent is pleased to support the open source community by making InjectFix available.
 * Copyright (C) 2019 THL A29 Limited, a Tencent company.  All rights reserved.
 * InjectFix is licensed under the MIT License, except for the third-party components listed in the file 'LICENSE' which may be subject to their corresponding license terms. 
 * This file is subject to the terms and conditions defined in file 'LICENSE', which is part of this source code package.
 */

namespace IFix.Core
{
    using System.Reflection;
    using System;
    using System.Linq.Expressions;

    public class ObjectClone
    {
#if ENABLE_IL2CPP
        DynamicBridge.Method memberwiseClone;
#else
        MethodInfo memberwiseClone;
        //Func<object> ptrToMemberwiseClone;
        //FieldInfo target;
        //Func<object, object> cloneFunc;
#endif
        public ObjectClone()
        {
            MethodInfo info= typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance
                | BindingFlags.NonPublic);
#if ENABLE_IL2CPP
            var flag = DynamicBridge.IL2CPPBridge.Flag.DB_BOXED_STRUCT_INSTANCE | DynamicBridge.IL2CPPBridge.Flag.DB_USING_IL2CPP_RUNTIME_INVOKER;
            DynamicBridge.IL2CPPBridge.GetMethod(info, out memberwiseClone, flag);
#else
            memberwiseClone = info;
            //ptrToMemberwiseClone = new Func<object>(MemberwiseClone);
            //target = ptrToMemberwiseClone.GetType().GetField("_target", BindingFlags.Instance
            //    | BindingFlags.NonPublic);
            //var methodInfo = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance
            //    | BindingFlags.NonPublic);
            //var p = Expression.Parameter(typeof(object), "obj");
            //var mce = Expression.Call(p, methodInfo);
            //cloneFunc = Expression.Lambda<Func<object, object>>(mce, p).Compile();//TODO: 需要用到jit么？
#endif
        }

        public object Clone(object obj)
        {
#if ENABLE_IL2CPP
            IntPtr ptr = DynamicBridge.IL2CPPBridge.ObjectToPointer(obj);
            ptr = DynamicBridge.Bridge.InvokeMethod<IntPtr, IntPtr>(ref memberwiseClone, ptr);
            return DynamicBridge.IL2CPPBridge.PointerToObject(ptr);
#else
            return memberwiseClone.Invoke(obj, null);//1.79s
            //target.SetValue(ptrToMemberwiseClone, obj);
            //return ptrToMemberwiseClone();//1.17s
            //return ((Func<object>)Delegate.CreateDelegate(typeof(Func<object>), obj, memberwiseClone))();//3.05s
            //return cloneFunc(obj);//0.06s
#endif
        }
    }
}