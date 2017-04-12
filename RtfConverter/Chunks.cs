using System;
using System.Collections;
using System.Collections.Generic;

namespace RtfConverter
{
    public class Chunks : Chunk, IList<Chunk>
    {
        private readonly IList<Chunk> chunks;
        public override AccumulatedFormat AccumulatedFormat { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        public Chunks()
        {
            chunks = new List<Chunk>();
        }

        public Chunk this[int index]
        {
            get
            {
                return chunks[index];
            }

            set
            {
                chunks[index] = value;
            }
        }

        public int Count => chunks.Count;

        public bool IsReadOnly => chunks.IsReadOnly;

        public override void Accept(IChunkVisitor visitor)
        {
            visitor.Visit(this);
        }

        public void Add(Chunk item)
        {
            chunks.Add(item);
        }

        public void Clear()
        {
            chunks.Clear();
        }

        public bool Contains(Chunk item)
        {
            return chunks.Contains(item);
        }

        public void CopyTo(Chunk[] array, int arrayIndex)
        {
            chunks.CopyTo(array, arrayIndex);
        }

        public IEnumerator<Chunk> GetEnumerator()
        {
            return chunks.GetEnumerator();
        }

        public int IndexOf(Chunk item)
        {
            return chunks.IndexOf(item);
        }

        public void Insert(int index, Chunk item)
        {
            chunks.Insert(index, item);
        }

        public bool Remove(Chunk item)
        {
            return chunks.Remove(item);
        }

        public void RemoveAt(int index)
        {
            chunks.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return chunks.GetEnumerator();
        }
    }
}
