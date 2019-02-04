namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class Bookmark : IDependency
    {
        Frame frame;

        public Bookmark(Frame frame, string name)
        {
            this.Name = name;
            this.frame = frame;
        }

        public Frame Frame
        {
            get { return this.frame; }
        }

        public string Name { get; private set; }

        public Object Object { get; set; }
    }
}
