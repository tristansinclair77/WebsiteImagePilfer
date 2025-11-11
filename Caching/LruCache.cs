using System;
using System.Collections.Generic;

namespace WebsiteImagePilfer.Caching
{
  /// <summary>
    /// Generic Least Recently Used (LRU) cache implementation.
    /// Automatically evicts least recently accessed items when capacity is reached.
 /// </summary>
    /// <typeparam name="TKey">The type of keys in the cache.</typeparam>
    /// <typeparam name="TValue">The type of values in the cache.</typeparam>
    public class LruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _maxCapacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cacheMap;
        private readonly LinkedList<CacheItem> _lruList;

    public LruCache(int maxCapacity)
        {
     if (maxCapacity <= 0)
                throw new ArgumentException("Max capacity must be greater than 0", nameof(maxCapacity));

       _maxCapacity = maxCapacity;
     _cacheMap = new Dictionary<TKey, LinkedListNode<CacheItem>>(maxCapacity);
         _lruList = new LinkedList<CacheItem>();
     }

    /// <summary>
        /// Gets the number of items currently in the cache.
     /// </summary>
  public int Count => _cacheMap.Count;

        /// <summary>
        /// Gets the maximum capacity of the cache.
    /// </summary>
     public int MaxCapacity => _maxCapacity;

  /// <summary>
        /// Attempts to get a value from the cache.
        /// </summary>
 /// <param name="key">The key to look up.</param>
        /// <param name="value">The value if found, otherwise default.</param>
   /// <returns>True if the key was found, false otherwise.</returns>
        public bool TryGetValue(TKey key, out TValue? value)
        {
          if (_cacheMap.TryGetValue(key, out var node))
          {
      // Move to front (most recently used)
      _lruList.Remove(node);
     _lruList.AddFirst(node);

      value = node.Value.Value;
return true;
      }

       value = default;
            return false;
        }

      /// <summary>
      /// Adds or updates a value in the cache.
        /// If capacity is reached, removes the least recently used item.
        /// </summary>
        /// <param name="key">The key to add or update.</param>
        /// <param name="value">The value to store.</param>
      public void Add(TKey key, TValue value)
        {
            if (_cacheMap.TryGetValue(key, out var existingNode))
            {
       // Update existing value and move to front
existingNode.Value.Value = value;
           _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
            }
            else
            {
      // Check capacity and evict if needed
 if (_cacheMap.Count >= _maxCapacity)
                {
      // Remove least recently used (last item)
     var lruNode = _lruList.Last;
if (lruNode != null)
          {
     _lruList.RemoveLast();
         _cacheMap.Remove(lruNode.Value.Key);
        }
 }

     // Add new item to front (most recently used)
         var cacheItem = new CacheItem(key, value);
        var newNode = new LinkedListNode<CacheItem>(cacheItem);
                _lruList.AddFirst(newNode);
      _cacheMap[key] = newNode;
            }
        }

        /// <summary>
        /// Removes a specific key from the cache.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns>True if the key was found and removed, false otherwise.</returns>
    public bool Remove(TKey key)
        {
            if (_cacheMap.TryGetValue(key, out var node))
         {
           _lruList.Remove(node);
   _cacheMap.Remove(key);
           return true;
          }

            return false;
}

/// <summary>
        /// Clears all items from the cache.
        /// </summary>
        public void Clear()
    {
            _cacheMap.Clear();
     _lruList.Clear();
        }

        /// <summary>
        /// Determines whether the cache contains the specified key.
    /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key exists in the cache, false otherwise.</returns>
    public bool ContainsKey(TKey key)
        {
            return _cacheMap.ContainsKey(key);
        }

  private class CacheItem
        {
            public TKey Key { get; }
    public TValue Value { get; set; }

        public CacheItem(TKey key, TValue value)
          {
        Key = key;
      Value = value;
    }
        }
    }
}
