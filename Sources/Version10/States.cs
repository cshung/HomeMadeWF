namespace HomeMadeWF
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;

    [Serializable]
    public class States
    {
        public Dictionary<string, Bookmark> bookmarks = new Dictionary<string, Bookmark>();
        public Collection<Frame> Frames = new Collection<Frame>();
        public int sequenceCounter;

        public States()
        {
            this.Frames.Add(new Frame(this, null));
        }

        public Action<Frame> ConsumeNextDelegate(out Frame frame)
        {
            frame = FindLastFrameWithWork();
            if (frame != null)
            {
                return frame.NextDelegates.Pop();
            }
            else
            {
                lock (bookmarks)
                {
                    Monitor.Wait(bookmarks);
                }
                return ConsumeNextDelegate(out frame);
            }
        }

        public bool HasMoreWork()
        {
            Collection<Frame> candidateToRemove = new Collection<Frame>();
            bool stop = false;
            do
            {
                candidateToRemove.Clear();
                foreach (Frame frame in this.Frames)
                {
                    if (frame.NextDelegates.Count == 0 && frame.NextDelegatesWithDependencies.Count == 0)
                    {
                        candidateToRemove.Add(frame);
                    }
                }
                if (candidateToRemove.Count == 0)
                {
                    stop = true;

                }
                else
                {
                    foreach (Frame frame in candidateToRemove)
                    {
                        if (frame.ParentFrame != null)
                        {
                            this.Frames.Add(frame.ParentFrame);
                            if (frame.ParentFrame.NextDelegatesWithDependencies.ContainsKey(frame))
                            {
                                Action<Frame> nextDelegate = frame.ParentFrame.NextDelegatesWithDependencies[frame];
                                frame.ParentFrame.NextDelegatesWithDependencies.Remove(frame);
                                if (nextDelegate != null)
                                {
                                    frame.ParentFrame.NextDelegates.Push(nextDelegate);
                                }
                            }
                        }
                        this.Frames.Remove(frame);
                    }
                }
            } while (!stop);
            return this.Frames.Count != 0;
        }

        Frame FindLastFrameWithWork()
        {
            Frame frameWithWork = null;
            foreach (Frame frame in this.Frames)
            {
                if (frame.NextDelegates.Count != 0)
                {
                    frameWithWork = frame;
                }
            }
            return frameWithWork;
        }

        public void ResumeBookmark(string bookmarkName, object data)
        {
            lock (this.bookmarks)
            {
                Bookmark bookmark = this.bookmarks[bookmarkName];
                bookmark.Object = data;
                Frame currentFrame = bookmark.Frame;
                if (currentFrame.NextDelegatesWithDependencies.ContainsKey(bookmark))
                {
                    Action<Frame> resumable = currentFrame.NextDelegatesWithDependencies[bookmark];
                    currentFrame.NextDelegatesWithDependencies.Remove(bookmark);
                    currentFrame.NextDelegates.Push(resumable);
                }
                Monitor.Pulse(this.bookmarks);
            }
        }
    }
}
