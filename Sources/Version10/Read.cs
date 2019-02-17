namespace HomeMadeWF
{
    using System;

    [Serializable]
    public class Read : Activity
    {
        public string BookmarkName { get; set; }

        public string ContentKey { get; set; }

        public override void Execute(Frame frame)
        {
            frame.WaitBookmark(this.BookmarkName, this.OnDataAvailable);
        }

        public void OnDataAvailable(Frame frame)
        {
            frame.ParentFrame.ParentFrame.Contents[this.ContentKey] = frame.States.bookmarks[BookmarkName].Object.ToString();
        }
    }
}
