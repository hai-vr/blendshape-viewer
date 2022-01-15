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

        public BlendshapeViewerDiffCompute()
        {
            _computeShader = FindComputeShader();
        }

        public Vector4 Compute(Texture2D textureA, Texture2D textureB)
        {
            var results = new int[4];
            var buf = new ComputeBuffer(4, sizeof(int));
            buf.SetData(results);
            var computeShader = _computeShader;

            var csMain = computeShader.FindKernel("DiffCompute");

            computeShader.SetTexture(csMain, "InputA", textureA);
            computeShader.SetTexture(csMain, "InputB", textureB);
            computeShader.SetBuffer(csMain, "ResultBuffer", buf);

            computeShader.Dispatch(csMain, textureA.width / 8, textureB.height / 8, 1);

            buf.GetData(results);
            return new Vector4(results[0], results[1], results[2], results[3]);
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
