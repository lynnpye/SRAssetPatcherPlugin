using System;
using System.Reflection;

namespace SRPlugin
{
    public class PrivateEye
    {
        public static void SetPrivateFieldValue(object obj, string fieldName, object value)
        {
            Type type = obj.GetType();
            FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                throw new ArgumentException($"Field '{fieldName}' not found in type '{type.FullName}'.");
            }

            field.SetValue(obj, value);
        }

        public static T GetPrivateFieldValue<T>(object obj, string fieldName, T defaultValue)
        {
            Type type = obj.GetType();
            FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
                throw new ArgumentException($"Field '{fieldName}' not found in type '{type.FullName}'.");
            }
            object fieldValue = field.GetValue(obj);
            if (fieldValue == null)
            {
                return defaultValue;
            }
            return (T)fieldValue;
        }

        public static T GetPrivateGetterValue<T>(object obj, string propertyName, T defaultValue)
        {
            Type type = obj.GetType();

            PropertyInfo propertyInfo = type.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (propertyInfo == null)
            {
                throw new ArgumentException($"Property '{propertyName}' not found on type '{type.FullName}'.");
            }

            MethodInfo getMethod = propertyInfo.GetGetMethod(true);

            if (getMethod == null)
            {
                throw new InvalidOperationException($"Property '{propertyName}' does not have a getter.");
            }

            // Invoke the getter method on the object
            return (T)getMethod.Invoke(obj, null);
        }
    }
}
