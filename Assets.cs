using System.Collections.Generic;
using UnityEngine;

namespace FrequencyWalkie
{
    public static class Assets
    {
        private static Dictionary<string, Object> _assets = new Dictionary<string, Object>();
        
        public static T GetResource<T>(string objName) where T : Object
        {
            if (_assets.TryGetValue(objName, out Object value))
            {
                return (T) value;
            }
            
            T[] objects = Resources.FindObjectsOfTypeAll<T>();
            foreach (T obj in objects)
            {
                if (obj.name == objName)
                {
                    _assets.Add(objName, obj);
                    return obj;
                }
            }
            return null;
        }
    }
}