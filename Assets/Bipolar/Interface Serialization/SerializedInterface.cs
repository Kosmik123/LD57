﻿using UnityEngine;

namespace Bipolar
{
    [System.Serializable]
    public class Serialized<TInterface> : Serialized<TInterface, Object>
        where TInterface : class
    { }
    
    [System.Serializable]
    public class Serialized<TInterface, TSerialized>
        where TInterface : class
        where TSerialized : Object
    {
        [SerializeField]
        private TSerialized serializedObject;

        private TInterface _value;
        public virtual TInterface Value
        {
            get
            {
                _value ??= serializedObject as TInterface;
                return _value;
            }
            set
            {
                if (!(value is TSerialized @object))
                    throw new System.InvalidCastException();

                serializedObject = @object;
                _value = value;
            }
        }

        public System.Type Type => typeof(TInterface);

        public static implicit operator TInterface(Serialized<TInterface, TSerialized> iface) => iface.Value;
    }
}
