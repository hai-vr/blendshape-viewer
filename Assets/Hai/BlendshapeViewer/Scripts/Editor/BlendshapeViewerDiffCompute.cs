/*
Copyright 2022 Pema Malling

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hai.BlendshapeViewer.Scripts.Editor
{
    public class BlendshapeViewerDiffCompute
    {
        private readonly ComputeShader _computeShader;
        private readonly ComputeBuffer _buf;
        private readonly int _kernel;

        public BlendshapeViewerDiffCompute()
        {
            _computeShader = FindComputeShader();
            _kernel = _computeShader.FindKernel("DiffCompute");
            _buf = new ComputeBuffer(4, sizeof(int));
            _computeShader.SetBuffer(_kernel, "ResultBuffer", _buf);
        }

        public Vector4 Compute(Texture2D textureA, Texture2D textureB)
        {
            var results = new int[4];
            _buf.SetData(results);
            var computeShader = _computeShader;

            computeShader.SetTexture(_kernel, "InputA", textureA);
            computeShader.SetTexture(_kernel, "InputB", textureB);

            computeShader.Dispatch(_kernel, textureA.width / 8, textureB.height / 8, 1);

            _buf.GetData(results);
            return new Vector4(results[0], results[1], results[2], results[3]);
        }

        public void Terminate()
        {
            _buf.Release();
        }

        private static ComputeShader FindComputeShader()
        {
            var assetPathOrEmpty = AssetDatabase.GUIDToAssetPath("569e5a4e6b0efc74b93a42db6d069724");
            var defaultPath = "Assets/Hai/BlendshapeViewer/Scripts/Editor/DiffCompute.compute";
            var computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(assetPathOrEmpty == "" ? defaultPath : assetPathOrEmpty)
                                ?? FindAmongAllComputeShaders();
            return computeShader;
        }

        private static ComputeShader FindAmongAllComputeShaders()
        {
            return Resources.FindObjectsOfTypeAll<ComputeShader>()
                .First(o => o.name.Contains("DiffCompute"));
        }
    }
}
