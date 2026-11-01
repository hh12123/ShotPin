using System;
using System.Collections.Generic;

namespace ShotPin.Core
{
    public sealed class LinkPair
    {
        public Guid A;
        public Guid B;

        public LinkPair(Guid a, Guid b)
        {
            A = a;
            B = b;
        }

        public bool Connects(Guid id)
        {
            return A == id || B == id;
        }

        public bool IsSamePair(Guid a, Guid b)
        {
            return (A == a && B == b) || (A == b && B == a);
        }
    }

    /// <summary>
    /// 图片之间的连线关系。纯数据，不涉及绘制。
    /// </summary>
    public sealed class LinkGraph
    {
        private readonly List<LinkPair> _links = new List<LinkPair>();

        public int Count
        {
            get { return _links.Count; }
        }

        public IList<LinkPair> All
        {
            get { return _links.AsReadOnly(); }
        }

        public bool Add(Guid a, Guid b)
        {
            if (a == Guid.Empty || b == Guid.Empty || a == b)
            {
                return false;
            }
            for (int i = 0; i < _links.Count; i++)
            {
                if (_links[i].IsSamePair(a, b))
                {
                    return false;
                }
            }
            _links.Add(new LinkPair(a, b));
            return true;
        }

        public bool Remove(Guid a, Guid b)
        {
            for (int i = 0; i < _links.Count; i++)
            {
                if (_links[i].IsSamePair(a, b))
                {
                    _links.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public int RemoveAll(Guid id)
        {
            int removed = 0;
            for (int i = _links.Count - 1; i >= 0; i--)
            {
                if (_links[i].Connects(id))
                {
                    _links.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        public bool HasLinks(Guid id)
        {
            for (int i = 0; i < _links.Count; i++)
            {
                if (_links[i].Connects(id))
                {
                    return true;
                }
            }
            return false;
        }

        public IList<Guid> Neighbors(Guid id)
        {
            List<Guid> result = new List<Guid>();
            for (int i = 0; i < _links.Count; i++)
            {
                LinkPair link = _links[i];
                if (link.A == id && !result.Contains(link.B))
                {
                    result.Add(link.B);
                }
                else if (link.B == id && !result.Contains(link.A))
                {
                    result.Add(link.A);
                }
            }
            return result;
        }

        public void Clear()
        {
            _links.Clear();
        }
    }
}
