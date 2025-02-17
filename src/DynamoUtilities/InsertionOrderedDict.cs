using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dynamo.Utilities;


namespace Dynamo.Utilities
{
    internal class InsertionOrderedDict<TKey, TValue> : IDictionary<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, LinkedListNode> _dictionary;

        private readonly LinkedList _values;
        private LinkedListNode? _head;
        private LinkedListNode? _tail;

        public InsertionOrderedDict()
        {
            _values = new LinkedList(this);
            _dictionary = new();
        }

        public InsertionOrderedDict(IEnumerable<KeyValuePair<TKey, TValue>> values)
        {
            ArgumentNullException.ThrowIfNull(values, nameof(values));

            _values = new LinkedList(this);

            if (values.TryGetNonEnumeratedCount(out var count))
            {
                _dictionary = new(count);
            }
            else
            {
                _dictionary = new();
            }

            foreach(var (key, value) in values)
            {
                // TODO: Do dictionaries throw when created from a key value pair enumerable?
                AddInner(key, value);
            }
        }

        public TValue this[TKey key]
        {
            get => _dictionary[key].Value;
            set => Add(key, value, true);
        }

        public ICollection<TKey> Keys => _dictionary.Keys;

        public ICollection<TValue> Values => _values;

        public int Count => _dictionary.Count;

        public bool IsReadOnly => false;

        private bool TryRemoveInner(TKey key, out LinkedListNode node)
        {
            if (!_dictionary.Remove(key, out node))
            {
                return false;
            }

            if (node.IsHead)
            {
                _head = node.Next;
                // if this is the only node, node.Next will be null, leaving _head as null
                if (_head == null)
                {
                    _tail = null;
                }
                else
                {
                    // node was .Prev, reset it
                    _head.Prev = null;
                }
            }
            else if (node.IsTail)
            {
                // the prev node must have a value, since node was not head
                _tail = node.Prev;
                _tail.Next = null;
            }
            else
            {
                // this node was between two other nodes, update them
                var next = node.Next;
                node.Prev.Next = next;
                next.Prev = node.Prev;
            }

            return true;
        }

        private void AddInner(TKey key, TValue value)
        {
            // adding the tail is always safe, as it should be empty if this is the first node
            var node = new LinkedListNode { Value = value, Prev = _tail };
            _dictionary.Add(key, node);

            // this only happens when adding the first node
            if (_head == null)
            {
                Debug.Assert(_tail == null);
                _head = _tail = node;
            }
            // _tail must be set if _head is set
            else
            {
                Debug.Assert(_tail != null && _head != null);
                _tail.Next = node;
                _tail = node;
            }
        }

        private void Add(TKey key, TValue value, bool overwrite)
        {
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            if (overwrite)
            {
                // for overwrite to work correctly, we need to remove any previous nodes with the same key
                // to ensure their linkedlist state gets set correctly, and also so that _dict.Add does
                // not throw
                TryRemoveInner(key, out _);
            }

            AddInner(key, value);
        }

        public void Add(TKey key, TValue value) => Add(key, value, false);

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value, false);

        public void Clear()
        {
            _dictionary.Clear();
            _head = null;
            _tail = null;
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return _dictionary.TryGetValue(item.Key, out var node) && item.Value.Equals(node);
        }

        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            var enumerable = _dictionary.Select(kv => new KeyValuePair<TKey, TValue>(kv.Key, kv.Value.Value));
            return enumerable.GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            ArgumentNullException.ThrowIfNull(key, nameof(key));

            return TryRemoveInner(key, out _);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            ArgumentNullException.ThrowIfNull(item.Key);

            return TryRemoveInner(item.Key, out _);
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            value = default;
            if (_dictionary.TryGetValue(key, out var node))
            {
                value = node.Value;
                return true;
            }
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private class LinkedListNode
        {
            public TValue Value;
            public LinkedListNode Prev = null;
            public LinkedListNode Next = null;

            public bool IsHead => Prev == null;
            public bool IsTail => Next == null;
        }

        private class LinkedListEnumerator : IEnumerator<TValue>
        {
            InsertionOrderedDict<TKey, TValue> _dict;
            bool _hasStarted = false;
            LinkedListNode? _current;

            public LinkedListEnumerator(InsertionOrderedDict<TKey, TValue> dict)
            {
                _dict = dict;
            }

            public TValue Current => _current == null ? default : _current.Value;

            object IEnumerator.Current => Current;

            public void Dispose() { }

            public bool MoveNext()
            {
                if (!_hasStarted)
                {
                    _hasStarted = true;
                    _current = _dict._head;
                    return _current != null;
                }
                else if (_current?.Next is LinkedListNode node)
                {
                    _current = node;
                    return true;
                }

                return false;
            }

            public void Reset() => throw new NotSupportedException();
        }

        private class LinkedList : ICollection<TValue>
        {
            private InsertionOrderedDict<TKey, TValue> _dict;

            public LinkedList(InsertionOrderedDict<TKey, TValue> dict)
            {
                _dict = dict;
            }

            public int Count => _dict.Count;

            public bool IsReadOnly => true;

            public void Add(TValue item) => throw new NotSupportedException();

            public void Clear() => throw new NotSupportedException();

            public bool Remove(TValue item) => throw new NotSupportedException();

            public void CopyTo(TValue[] array, int arrayIndex)
            {
                foreach(var value in this)
                {
                    array[arrayIndex++] = value;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public IEnumerator<TValue> GetEnumerator() => new LinkedListEnumerator(_dict);

            public bool Contains(TValue item)
            {
                foreach(var value in this)
                {
                    if (EqualityComparer<TValue>.Default.Equals(item, value))
                    {
                        return true;
                    }
                }

                return false;
            }

        }
    }
}
