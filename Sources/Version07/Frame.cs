namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class Frame
    {
        public Action<States> NextDelegate { get; set; }
    }
}