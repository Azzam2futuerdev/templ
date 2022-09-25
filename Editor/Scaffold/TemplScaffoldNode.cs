﻿/*
 * Copyright (c) 2022 Willy Alberto Kuster
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Willykc.Templ.Editor.Scaffold
{
    [Serializable]
    internal abstract class TemplScaffoldNode
    {
        public string name;
        [SerializeReference]
        public List<TemplScaffoldNode> children = new List<TemplScaffoldNode>();
        [SerializeReference]
        public TemplScaffoldNode parent;

        internal TemplScaffoldNode Clone()
        {
            var clone = CloneRecursive(this, parent);
            parent?.children.Add(clone);
            return clone;
        }

        internal bool IsValid =>
            IsValidNode &&
            !IsNameDuplicated &&
            !string.IsNullOrWhiteSpace(name) &&
            name.IndexOfAny(Path.GetInvalidFileNameChars()) == -1 &&
            children.All(c => c.IsValid);

        protected virtual bool IsValidNode => true;

        internal bool ContainsTemplate(ScribanAsset template)
        {
            if(this is TemplScaffoldFile fileNode && fileNode.template == template)
            {
                return true;
            }
            return children.Any(c => c.ContainsTemplate(template));
        }

        protected abstract TemplScaffoldNode DoClone();

        private TemplScaffoldNode CloneRecursive(
            TemplScaffoldNode original,
            TemplScaffoldNode parent)
        {
            var clone = DoClone();
            clone.name = original.name;
            clone.parent = parent;
            clone.children.AddRange(original.children.Select(c => c.CloneRecursive(c, clone)));
            return clone;
        }

        private bool IsNameDuplicated =>
            parent?.children.Any(c => c != this && c.name == name) ?? false;
    }
}
