﻿using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading;

namespace HAMT
{
    [DebuggerDisplay("Nodes = {_count}")]
    public partial class HAMTrie<T> : TrieNode
    {
        #region Constants

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        const int SIZE = 6;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        const int BUCKET_SIZE = 64;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        const uint MASK = BUCKET_SIZE - 1;

        #endregion


        #region Fields

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _count;
        
        #endregion


        #region Constructors

        public HAMTrie()
        {
            Nodes = new INode[BUCKET_SIZE];
            Bitmap = ulong.MaxValue;
            Array.Fill(Nodes, new TrieNode());
        }

        #endregion


        public int Length => _count;


        public ref T this[uint hash]
        {
            get 
            {
                Leaf leaf;
                TrieNode childNode;
                TrieNode parentNode = this;
                TrieNode activeNode = this;
                INode formerNode;


                do
                {
                    int index = 0;
                    var shift = hash;
                    var position = (ulong)1 << (int)(hash & MASK);

                    int parentIndex = 0;

                    while ((activeNode.Bitmap & position) != 0)
                    {
                        index = BitOperations.PopCount(position - 1 & activeNode.Bitmap);
                        var node = activeNode.Nodes[index];

                        if (node.IsLeaf)
                        {
                            leaf = (Leaf)node;

                            if (leaf.Hash == shift) return ref leaf.Value;

                            var other = new Leaf(shift);
                            
                            formerNode = Interlocked.CompareExchange(ref parentNode.Nodes[parentIndex],
                                GetSplitNode(leaf, other), activeNode);

                            // Return on success
                            if (true == ReferenceEquals(formerNode, activeNode))
                            {
                                Interlocked.Increment(ref _count);
                                return ref other.Value;
                            }

                            // Roll back and start over if failed
                            activeNode = (TrieNode)formerNode;
                            continue;
                        }

                        parentNode = activeNode;
                        parentIndex = index;
                        activeNode = (TrieNode)activeNode.Nodes[index];

                        shift >>= SIZE;
                        position = (ulong)1 << (int)(shift & MASK);
                    }

                    // Calculate metadata
                    var flags = activeNode.Bitmap | position;
                    var length = activeNode.Nodes.Length;
                    var nodes = new INode[length + 1];
                    var point = BitOperations.PopCount(position - 1 & flags);

                    // Copy other entries if required
                    if (0 < point) Array.Copy(activeNode.Nodes, nodes, point);
                    if (length > point) Array.Copy(activeNode.Nodes, point, nodes, point + 1, length - point);

                    // Add leaf node to array
                    leaf = new Leaf(shift);
                    nodes[point] = leaf;
                    childNode  = new TrieNode(flags, nodes);
                    formerNode = Interlocked.CompareExchange(ref parentNode.Nodes[index], childNode, activeNode);

                } while (false == ReferenceEquals(formerNode, activeNode));

                Interlocked.Increment(ref _count);

                return ref leaf.Value; 
            }
        }


        public T? this[T key]
        { 
            get 
            {
                var hash = key?.GetHashCode() ?? 0x0;
                var position = (ulong)1 << (int)(hash & MASK);
                TrieNode activeNode = this;

                while ((activeNode.Bitmap & position) != 0)
                {
                    var index = BitOperations.PopCount(position - 1 & activeNode.Bitmap);
                    var node = activeNode.Nodes[index];

                    if (node.IsLeaf)
                    {
                        var leaf = (Leaf)node;
                        if (leaf.Hash == hash) return leaf.Value;
                    }

                    activeNode = (TrieNode)activeNode.Nodes[index];

                    hash >>= SIZE;
                    position = (ulong)1 << (int)(hash & MASK);
                }

                return default;
            }
            set 
            { 
            }
        }
    }
}
