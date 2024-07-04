using System;

namespace SRPlugin
{
    public class OverrideableValue<T>
    {
        private bool wasSet;
        private Action<T> setter;
        private T originalValue;
        private T defaultSetValue;

        public OverrideableValue(T originalValue, Action<T> setter, T defaultSetValue = default)
        {
            this.originalValue = originalValue;
            this.setter = setter;
            this.defaultSetValue = defaultSetValue;
        }

        public void Reset()
        {
            if (!wasSet) return;
            wasSet = false;
            setter(originalValue);
        }

        public void Set(T value)
        {
            wasSet = true;
            setter(value);
        }

        public void SetDefault()
        {
            Set(defaultSetValue);
        }
    }
}
