using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace S3Unity.STrail
{
    [ExecuteInEditMode]
    public class STrail : MonoBehaviour
    {
        const int SEGMENT_EMPTY = int.MaxValue;

        public class Element
        {
            public Element(Vector3 _position,float _width,float _texCoord,Color _colour)
            {
                position = _position;
                width = _width;
                texCoord = _texCoord;
                colour = _colour;
                now = 0f;
            }

            public Vector3 position;
            public float width, texCoord;
            public Color colour;
            //public Vector3 normal;
            //public Vector3 tangent;
            public float now;
        }
        public enum Direction
        {
            TCD_V,
            TCD_U
        }


        public struct ChainSegment
        {
            public int start;
            public int head;
            public int tail;
        }

        struct Vertex
        {
            public Vector3 position;
            public Vector2 uv;
            public Vector2 uv1;
            public Color color;
        }
        [Header("Order1_LOD0")]
        public int BillboardType = 0;
        //public int NumberOfChains = 1;//导出用暂无实际影响
        public int MaxChainElements = 1;
        public int MaxSegments = 1;
        public int LifeTime = 1;
        public float TrailLength = 1.0f;
        public float TexCoordLength = 1.0f;
        public float TexCoord1Length = 1.0f;
        public Direction TexCoordDirection = Direction.TCD_U;
        public bool TexCoordHeadFixed = false;
        public bool TexCoord1HeadFixed = false;
        public float RandomUVOffset = 0.0f;
        public float RandomUV1Offset = 0.0f;
        public float TimeChange = 1f;
        public Gradient GradientColor = new Gradient();
        //public AnimationCurve CurveWidthChange = AnimationCurve.Linear(0, 1, 0, 1);
        public ParticleSystem.MinMaxCurve CurveWidthChange = new ParticleSystem.MinMaxCurve();


        [HideInInspector]
        public int TheChangeType = 1;
        [HideInInspector]
        public Color InitialColor = Color.white;
        /*[HideInInspector]
        public Color ColorChange = Color.clear;
        [HideInInspector]
        public Color ColorChange1 = Color.clear;*/
        [HideInInspector]
        public float InitialWidth = 1.0f;
        /*[HideInInspector]
        public float WidthChange = 1.0f;
        [HideInInspector]
        public float WideChange1 = 0.0f;*/
        public Vector3 Offset = Vector3.zero;
        public bool Decal = false;

        [Header("Chains_0")]
        public Vector3 Point_0=Vector3.zero;
        public Vector3 Point_1=Vector3.zero;

        [Header("Material")]
        public UnityEngine.Rendering.ShadowCastingMode CastShadows = UnityEngine.Rendering.ShadowCastingMode.On;
        public bool ReceiveShadows = true;
        public bool UseLightProbes = true;

        List<Element> mChainElementList;
        List<Vertex> mVertexData;
        List<int> mIndexBuffer;
        List<CTrailBoneChain> mVecTrailBoneChain;
        ChainSegment seg;
        float mElemLength;
        float mSquaredElemLength;
        float mPreTexcoord = 0f;
        float[] mOtherTexCoordRange = { 0f, 1f };
        float[] mFinalRandomUVOffset;
        float mNowUvOffert;
        float mNowUv1Offset;
        Mesh mTrailMesh;
        bool mIndexNeedChange = true;
        int mIndexCount;
        int mTimeCount;
        int mMaxSegmentsRecord;
        int mBillBoardTypeRecord;
        LineRenderer mLineRenderer;
        Material LineMaterial;
        GameObject LineEmptyObject;
        GameObject pointHelperPrefab;
        GameObject PointHelper0;
        GameObject PointHelper1;

        private void Update()
        {
            RenderTrailMesh();
        }

        private void LateUpdate()
        {
            //InspectorChange();
            TimeUpdate();
        }

        private void Awake()
        {
            //SetupChainContains();
        }

        private void OnEnable()
        {
            if (gameObject.GetComponent<MeshRenderer>() == null)
                gameObject.AddComponent<MeshRenderer>();
            mTrailMesh = new Mesh();
            mTrailMesh.name = "S3_TrailMesh";
            mTrailMesh.MarkDynamic();
            pointHelperPrefab = Resources.Load<GameObject>("Prefabs/PointHelper");
            LineMaterial = Resources.Load<Material>("Materials/LineHelper");
            mMaxSegmentsRecord = MaxSegments;
            mBillBoardTypeRecord = BillboardType;
            CleanHelper();
            SetInitialData();
            SetupChainContains();
            if (BillboardType == 2 && mLineRenderer == null)
            {
                SetLineHelper();
            }
            ResetElement();
            SetupOffset();
            //Camera.onPreCull += RenderTrailMesh;
        }

        void OnDisable()
        {
            DestroyImmediate(mTrailMesh);
            DestroyLineHelper();
            //Camera.onPreCull -= RenderTrailMesh;
        }

        public void InspectorChange()
        {
            if(BillboardType!=mBillBoardTypeRecord)
            {
                if(BillboardType<0||BillboardType>2)
                {
                    BillboardType = mBillBoardTypeRecord;
                }
                else
                {
                    CleanList();
                    SetupChainContains();
                    ResetElement();
                    mBillBoardTypeRecord = BillboardType;
                }
            }
            if (mChainElementList.Count != MaxChainElements)
            {
                CleanList();
                SetupChainContains();
                ResetElement();
            }
            if (mNowUvOffert != RandomUVOffset || mNowUv1Offset != RandomUV1Offset)
            {
                SetupOffset();
            }
            if (!Mathf.Approximately(mElemLength, TrailLength / MaxChainElements))
            {
                mElemLength = TrailLength / MaxChainElements;
                mSquaredElemLength = mElemLength * mElemLength;
            }
            if (BillboardType == 2)
            {
                if (mLineRenderer == null)
                {
                    SetLineHelper();
                }
                else
                {
                    if (!Mathf.Approximately(Vector3.Distance(Point_0, PointHelper0.transform.localPosition), 0))
                    {
                        PointHelper0.transform.localPosition = Point_0;
                    }
                    if (!Mathf.Approximately(Vector3.Distance(Point_1, PointHelper1.transform.localPosition), 0))
                    {
                        PointHelper1.transform.localPosition = Point_1;
                    }
                }

                if(MaxSegments!=mMaxSegmentsRecord)
                {
                    MaxSegmentChange();
                }
            }
            else if (BillboardType != 2 && mLineRenderer != null)
            {
                DestroyLineHelper();
            }
        }

        private void CleanList()
        {
            mChainElementList.Clear();
            mVertexData.Clear();
            mIndexBuffer.Clear();
            mVecTrailBoneChain.Clear();
        }

        private void SetupChainContains()
        {
            mChainElementList = new List<Element>();
            Element[] chain = new Element[MaxChainElements];
            for (int i = 0; i < MaxChainElements; ++i)
            {
                chain[i] = new Element(transform.position, 0, 0, Color.clear);
            }
            mChainElementList.AddRange(chain);

            mVecTrailBoneChain = new List<CTrailBoneChain>();
            CTrailBoneChain boneChain = new CTrailBoneChain(MaxChainElements, Point_0, Point_1);
            mVecTrailBoneChain.Add(boneChain);

            mVertexData = new List<Vertex>();
            mIndexBuffer = new List<int>();
            SetUpBuffers();

            seg.start = 0;
            seg.head = seg.tail = SEGMENT_EMPTY;

            mElemLength = TrailLength / MaxChainElements;
            mSquaredElemLength = mElemLength * mElemLength;
        }

        private void SetupOffset()
        {
            mNowUvOffert = RandomUVOffset;
            mNowUv1Offset = RandomUV1Offset;
            mFinalRandomUVOffset = new float[2];
            mFinalRandomUVOffset[0] = Random.Range(0, RandomUVOffset);
            mFinalRandomUVOffset[1] = Random.Range(0, RandomUV1Offset);
        }

        private void AddChainElement(int chainIndex, Element elem)
        {
            if (seg.head == SEGMENT_EMPTY)
            {
                seg.tail = MaxChainElements - 1;
                seg.head = seg.tail;
                mIndexNeedChange = true;
            }
            else
            {
                if (seg.head == 0)
                {
                    seg.head = MaxChainElements - 1;
                }
                else
                {
                    --seg.head;
                }

                if (seg.head == seg.tail)
                {
                    if (seg.tail == 0)
                    {
                        seg.tail = MaxChainElements - 1;
                    }
                    else
                    {
                        --seg.tail;
                    }
                }
            }

            mChainElementList[seg.start + seg.head] = elem;

            if (BillboardType > 1 && mVecTrailBoneChain.Count > chainIndex)//bone
            {
                mVecTrailBoneChain[chainIndex].UpdatePos(seg.head, elem.position, transform.localScale, transform.rotation);
                if (mVecTrailBoneChain[chainIndex].GetSegTail() < MaxSegments)
                    mVecTrailBoneChain[chainIndex].SetSegTail(mVecTrailBoneChain[chainIndex].GetSegTail() + MaxChainElements / MaxChainElements);

            }
            mIndexNeedChange = true;
        }

        private void UpdateVertexList(/*Camera cam, */float fScale = 1.0f)
        {
#if UNITY_EDITOR
            Vector3 eyePos = SceneView.lastActiveSceneView.camera.transform.position;
#else
            Vector3 eyePos = Camera.main.transform.position;
#endif
            Vector3 chainTangent;

            if (BillboardType > 1 && LifeTime < 9999999)
            {
                mTimeCount += (int)(Time.fixedDeltaTime * 1000);
            }

            if (BillboardType > 1)
                mVecTrailBoneChain[0].SetHeadAndTail(seg.head, seg.tail);

            if (seg.head != SEGMENT_EMPTY && seg.head != seg.tail)
            {
                int laste = seg.head;
                float fCurLength = 0f;
                float fCurLength1 = 0f;
                if (BillboardType <= 1)
                {
                    for (int e = seg.head; ; e++)
                    {
                        if (e == MaxChainElements)
                            e = 0;
                        Element elem = mChainElementList[e + seg.start];
                        int baseidx = (e + seg.start) * 2;

                        int nexte = e + 1;
                        if (nexte == MaxChainElements)
                            nexte = 0;

                        if (e == seg.head)
                        {
                            chainTangent = mChainElementList[nexte + seg.start].position - elem.position;
                        }
                        else if (e == seg.tail)
                        {
                            chainTangent = elem.position - mChainElementList[laste + seg.start].position;
                        }
                        else
                        {
                            chainTangent = mChainElementList[nexte + seg.start].position - elem.position;
                        }

                        int i = 0;
                        if (chainTangent.x != 0)
                        {
                            i++;
                        }

                        float fUV;
                        if (TexCoordHeadFixed)
                        {
                            if (e == seg.head)
                                fCurLength = 0f;
                            else
                                fCurLength += Mathf.Sqrt(chainTangent.sqrMagnitude);

                            fUV = mFinalRandomUVOffset[0] + fCurLength / TexCoordLength;
                        }
                        else
                        {
                            fUV = mFinalRandomUVOffset[0] + elem.texCoord / TexCoordLength;
                        }

                        float fUV1;
                        if (TexCoord1HeadFixed)
                        {
                            if (e == seg.head)
                                fCurLength1 = 0f;
                            else
                                fCurLength1 += Mathf.Sqrt(chainTangent.sqrMagnitude);

                            fUV1 = mFinalRandomUVOffset[1] + fCurLength / TexCoord1Length;
                        }
                        else
                        {
                            fUV1 = mFinalRandomUVOffset[1] + elem.texCoord / TexCoord1Length;
                        }

                        Vector3 vP1ToEye;
                        Vector3 vPerpendicular = chainTangent;

                        if (BillboardType == 0)
                        {
                            vP1ToEye = Vector3.up;
                            vPerpendicular = Vector3.Cross(chainTangent, vP1ToEye);
                        }
                        if (BillboardType == 1)
                        {
                            vP1ToEye = eyePos - elem.position;
                            vPerpendicular = Vector3.Cross(chainTangent, vP1ToEye);
                        }

                        if (BillboardType == 0 || BillboardType == 1)
                        {
                            vPerpendicular.Normalize();
                            vPerpendicular *= (elem.width * 0.5f * fScale);

                        }

                        Vector3 pos0 = elem.position - vPerpendicular;
                        Vector3 pos1 = elem.position + vPerpendicular;

                        Vertex vertex;
                        Vertex vertex2;
                        vertex.position = pos0;
                        vertex2.position = pos1;
                        if (TexCoordDirection == Direction.TCD_U)
                        {
                            vertex.uv = new Vector2(fUV, mOtherTexCoordRange[0]);
                            vertex.uv1 = new Vector2(fUV1, mOtherTexCoordRange[0]);
                            vertex2.uv = new Vector2(fUV, mOtherTexCoordRange[1]);
                            vertex2.uv1 = new Vector2(fUV1, mOtherTexCoordRange[1]);

                        }
                        else
                        {
                            vertex.uv = new Vector2(mOtherTexCoordRange[0], fUV);
                            vertex.uv1 = new Vector2(mOtherTexCoordRange[0], fUV1);
                            vertex2.uv = new Vector2(mOtherTexCoordRange[1], fUV);
                            vertex2.uv1 = new Vector2(mOtherTexCoordRange[1], fUV1);
                        }
                        vertex.color = elem.colour;
                        vertex2.color = elem.colour;

                        mVertexData[baseidx] = vertex;
                        mVertexData[baseidx + 1] = vertex2;

                        if (e == seg.tail)
                            break;
                        laste = e;
                    }
                }//BillBoardType<=1
                else
                {
                    if (LifeTime < 9999999)
                    {
                        if (mTimeCount > LifeTime && mVecTrailBoneChain[0].GetSegTail() > 0)
                        {
                            mVecTrailBoneChain[0].SetSegTail(mVecTrailBoneChain[0].GetSegTail() - mTimeCount / LifeTime);
                            mIndexNeedChange = true;
                        }
                    }
                    int nTrailLength = GetLength(seg);
                    nTrailLength = (int)(nTrailLength / (float)MaxChainElements * MaxSegments);
                    int nSegTail = nTrailLength - mVecTrailBoneChain[0].GetSegTail();
                    nSegTail = nSegTail > 0 ? nSegTail : 0;

                    int baseIdx = 0;
                    float fPerLength = 1.0f / (float)nTrailLength;
                    float fPercent = 1.0f;
                    float fPercentToLast = 0;
                    //Vector3 segPosLast;
                    for (int i = nTrailLength; i >= nSegTail; --i)
                    {
                        Vector3 segPos = new Vector3();
                        Vector3 segHalfDir = new Vector3();
                        int nLast = GetIndex(seg, fPercent, ref fPercentToLast);
                        mVecTrailBoneChain[0].GetSegmentPosAndDir(nLast, fPercentToLast, ref segPos, ref segHalfDir);
                        float fUVPercent = fPercent;
                        fPercent -= fPerLength;
                        Element elem = mChainElementList[nLast + seg.start];
                        if (nTrailLength > nSegTail)
                        {
                            fUVPercent = (float)(i - nSegTail) / (nTrailLength - nSegTail);
                        }
                        float fUV = mFinalRandomUVOffset[0] + 1.0f - fUVPercent;
                        float fUV1 = mFinalRandomUVOffset[1] + 1.0f - fUVPercent;
                        if (!TexCoordHeadFixed)
                        {
                            fUV = mFinalRandomUVOffset[0] + elem.texCoord / TexCoordLength;
                        }
                        if (!TexCoord1HeadFixed)
                        {
                            fUV1 = mFinalRandomUVOffset[1] + elem.texCoord / TexCoord1Length;
                        }

                        Vector3 pos0 = Vector3.zero;
                        Vector3 pos1 = Vector3.zero;

                        if (BillboardType == 2)
                        {
                            segHalfDir.Normalize();
                            segHalfDir *= elem.width * 0.5f * fScale * mVecTrailBoneChain[0].GetLength();

                            pos0 = segPos - segHalfDir;
                            pos1 = segPos + segHalfDir;
                        }

                        int a = mVertexData.Count;

                        Vertex vertex = mVertexData[baseIdx];
                        Vertex vertex1 = mVertexData[baseIdx + 1];
                        Vertex vertex2 = mVertexData[baseIdx + 2];
                        vertex.position = pos0;
                        vertex1.position = segPos;
                        vertex2.position = pos1;

                        if (TexCoordDirection == Direction.TCD_U)
                        {
                            vertex.uv = new Vector2(fUV, mOtherTexCoordRange[0]);
                            vertex.uv1 = new Vector2(fUV1, mOtherTexCoordRange[0]);

                            vertex1.uv = new Vector2(fUV, 0.5f);
                            vertex1.uv1 = new Vector2(fUV1, 0.5f);

                            vertex2.uv = new Vector2(fUV, mOtherTexCoordRange[1]);
                            vertex2.uv1 = new Vector2(fUV1, mOtherTexCoordRange[1]);
                        }
                        else
                        {
                            vertex.uv = new Vector2(mOtherTexCoordRange[0], fUV);
                            vertex.uv1 = new Vector2(mOtherTexCoordRange[0], fUV1);

                            vertex1.uv = new Vector2(0.5f, fUV);
                            vertex1.uv1 = new Vector2(0.5f, fUV1);

                            vertex2.uv = new Vector2(mOtherTexCoordRange[1], fUV);
                            vertex2.uv1 = new Vector2(mOtherTexCoordRange[1], fUV1);
                        }
                        vertex.color = elem.colour;
                        vertex1.color = elem.colour;
                        vertex2.color = elem.colour;

                        mVertexData[baseIdx] = vertex;
                        mVertexData[baseIdx + 1] = vertex1;
                        mVertexData[baseIdx + 2] = vertex2;

                        baseIdx += 3;
                    }
                }//end if(BillBoardType<=1)
            }
            if (BillboardType > 1 && LifeTime < 9999999 && mTimeCount > LifeTime)
            {
                mTimeCount = 0;
            }
        }

        private void UpdateIndexBuffer()
        {
            if (mIndexNeedChange)
            {
                mIndexCount = 0;

                if (seg.head != SEGMENT_EMPTY && seg.head != seg.tail)
                {
                    if (BillboardType <= 1)
                    {
                        int laste = seg.head;
                        while (true)
                        {
                            int e = laste + 1;
                            if (e == MaxChainElements)
                                e = 0;
                            int baseIdx = (e + seg.start) * 2;
                            int lastBaseIdx = (laste + seg.start) * 2;
                            mIndexBuffer[mIndexCount] = lastBaseIdx;
                            mIndexBuffer[mIndexCount + 1] = lastBaseIdx + 1;
                            mIndexBuffer[mIndexCount + 2] = baseIdx;
                            mIndexBuffer[mIndexCount + 3] = lastBaseIdx + 1;
                            mIndexBuffer[mIndexCount + 4] = baseIdx + 1;
                            mIndexBuffer[mIndexCount + 5] = baseIdx;
                            mIndexCount += 6;

                            if (e == seg.tail)
                                break;

                            laste = e;
                        }
                    }
                    else
                    {
                        int nTrailLength = GetLength(seg);
                        nTrailLength = (int)(((float)nTrailLength) / (float)MaxChainElements * MaxSegments);
                        int nSegTail = nTrailLength - mVecTrailBoneChain[0].GetSegTail();
                        nSegTail = nSegTail > 0 ? nSegTail : 0;

                        int baseIdxOffset = 0;

                        nTrailLength -= nSegTail;

                        for (int i = 0; i < nTrailLength; ++i)
                        {
                            int baseIdx = i * 3 + baseIdxOffset;
                            int lastBaseIdx = (i + 1) * 3 + baseIdxOffset;
                            mIndexBuffer[mIndexCount] = lastBaseIdx + 1;
                            mIndexBuffer[mIndexCount + 1] = lastBaseIdx + 2;
                            mIndexBuffer[mIndexCount + 2] = baseIdx + 1;
                            mIndexBuffer[mIndexCount + 3] = lastBaseIdx + 2;
                            mIndexBuffer[mIndexCount + 4] = baseIdx + 2;
                            mIndexBuffer[mIndexCount + 5] = baseIdx + 1;

                            mIndexBuffer[mIndexCount + 6] = lastBaseIdx;
                            mIndexBuffer[mIndexCount + 7] = lastBaseIdx + 1;
                            mIndexBuffer[mIndexCount + 8] = baseIdx;
                            mIndexBuffer[mIndexCount + 9] = lastBaseIdx + 1;
                            mIndexBuffer[mIndexCount + 10] = baseIdx + 1;
                            mIndexBuffer[mIndexCount + 11] = baseIdx;

                            mIndexCount += 12;
                        }
                    }
                }
                mIndexNeedChange = false;
            }
        }

        private void UpdateTrail()
        {
            bool done = false;
            Vector3 newPos = transform.position;
            int nLoopTimes = 0;
            while (!done)
            {
                int head = seg.head;
                Element headElem = mChainElementList[seg.start + seg.head];
                int nextElemIdx = seg.head + 1;
                if (nextElemIdx == MaxChainElements)
                    nextElemIdx = 0;
                Element nextElem = mChainElementList[seg.start + nextElemIdx];

                Vector3 diff = newPos - nextElem.position;
                float sqlen = diff.sqrMagnitude;
                float fDiffLen = Mathf.Sqrt(sqlen);

                if (fDiffLen > mElemLength)
                {
                    {
                        Vector3 scaledDiff = diff * (mElemLength / fDiffLen);

                        if (BillboardType > 1)
                        {
                            float fAdd = fDiffLen / mElemLength;
                            int nAdd = (int)fAdd;
                            float fTexElem = TexCoordLength / (float)MaxChainElements;

                            if (nAdd <= MaxChainElements)
                            {
                                headElem.position = nextElem.position + scaledDiff;
                                headElem.texCoord = nextElem.texCoord + fTexElem;
                            }
                            else
                            {
                                nAdd = MaxChainElements;
                                diff = diff * (TrailLength / fDiffLen);
                                headElem.position = newPos - diff;
                                headElem.texCoord = 0;
                            }
                            scaledDiff = diff * (1.0f / (float)nAdd);

                            mVecTrailBoneChain[0].UpdatePos(seg.head, headElem.position, transform.localScale, transform.rotation);
                            for (int i = 1; i < nAdd; i++)
                            {
                                Element ltHeadElem = mChainElementList[seg.start + seg.head];
                                Element ltElem = new Element(ltHeadElem.position + scaledDiff, InitialWidth, ltHeadElem.texCoord + fTexElem, GradientColor.Evaluate(0));
                                AddChainElement(0, ltElem);
                            }
                            float fEx = fAdd - nAdd;

                            Element NewheadElem = mChainElementList[seg.start + seg.head];
                            NewheadElem.position = newPos;
                            NewheadElem.texCoord += fTexElem * fEx;
                            mVecTrailBoneChain[0].UpdatePos(seg.head, NewheadElem.position, transform.localScale, transform.rotation);

                            break;
                        }

                        headElem.position = nextElem.position + scaledDiff;

                        if (headElem.position == nextElem.position)
                        {
                            nextElem.position = headElem.position = newPos;
                            break;
                        }

                        if (nLoopTimes++ > MaxChainElements * 3)
                        {
                            headElem.position = newPos - diff * (TrailLength * 2 / fDiffLen);
                            nLoopTimes = 0;
                        }
                        mChainElementList[seg.start + seg.head] = headElem;
                        mChainElementList[nextElemIdx] = nextElem;
                    }

                    mPreTexcoord += mElemLength;
                    Element newElem = new Element(newPos, InitialWidth, mPreTexcoord, GradientColor.Evaluate(0));
                    AddChainElement(0, newElem);
                    diff = newPos - mChainElementList[head].position;
                    if (diff.sqrMagnitude <= mSquaredElemLength)
                        done = true;
                }
                else
                {
                    headElem.position = newPos;
                    mChainElementList[head] = headElem;
                    done = true;
                }

                if ((seg.tail + 1) % MaxChainElements == seg.head)
                {
                    // If so, shrink tail gradually to match head extension
                    Element tailElem = mChainElementList[seg.start + seg.tail];
                    int preTailIdx;
                    if (seg.tail == 0)
                        preTailIdx = MaxChainElements - 1;
                    else
                        preTailIdx = seg.tail - 1;
                    Element preTailElem = mChainElementList[seg.start + preTailIdx];

                    // Measure tail diff from pretail to tail
                    Vector3 taildiff = tailElem.position - preTailElem.position;
                    float taillen = taildiff.magnitude;
                    if (taillen > 1e-06)
                    {
                        float tailsize = mElemLength - diff.magnitude;
                        taildiff *= tailsize / taillen;
                        //tailElem.position = preTailElem.position + taildiff;

                        if (BillboardType == 2)//bone
                        {
                            mVecTrailBoneChain[0].UpdateTailPos(seg.tail, preTailIdx, tailsize / taillen);
                        }
                    }
                }
            }
        }

        private void CommitMesh()
        {
            List<Vector3> normals = new List<Vector3>();
            List<Vector3> tangents = new List<Vector3>();
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector2> uv1s = new List<Vector2>();
            List<Color> vertColors = new List<Color>();
            List<int> index = new List<int>();

            for (int i = 0; i < mVertexData.Count; ++i)
            {
                vertices.Add(mVertexData[i].position + Offset);
                uvs.Add(mVertexData[i].uv);
                uv1s.Add(mVertexData[i].uv1);
                vertColors.Add(mVertexData[i].color);
            }
            for (int i = 0; i < mIndexCount; ++i)
            {
                index.Add(mIndexBuffer[i]);
            }

            mTrailMesh.SetVertices(vertices);
            mTrailMesh.SetColors(vertColors);
            mTrailMesh.SetUVs(0, uvs);
            mTrailMesh.SetUVs(1, uv1s);
            mTrailMesh.SetTriangles(index, 0, true);
        }

        private void RenderTrailMesh()
        {
            if (BillboardType == 2 && mLineRenderer != null)
                UpdateLineHelper();
            mTrailMesh.Clear();
            UpdateTrail();
            UpdateVertexList();
            UpdateIndexBuffer();
            CommitMesh();
            List<MaterialPropertyBlock> mtlProperties = new List<MaterialPropertyBlock>();
            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            Material[] Materials = meshRenderer.sharedMaterials;
            for (int i = 0; i < Materials.Length; ++i)
            {
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(propertyBlock);
                mtlProperties.Add(propertyBlock);
            }

            for (int i = 0; i < Materials.Length; ++i)
            {
                Graphics.DrawMesh(mTrailMesh, /*space == Space.Self && transform.parent != null ? transform.parent.localToWorldMatrix : */Matrix4x4.identity,
                                  Materials[i], gameObject.layer, null, 0, mtlProperties[i], CastShadows, ReceiveShadows, null, UseLightProbes);
            }
        }

        private void TimeUpdate()
        {
            if (seg.head != SEGMENT_EMPTY && seg.head != seg.tail)
            {
                for (int e = seg.head; ; ++e)
                {
                    e = e % MaxChainElements;
                    Element elem = mChainElementList[seg.start + e];

                    if ((elem.now + Time.fixedDeltaTime) <= TimeChange)
                    {
                        elem.now += Time.fixedDeltaTime;
                        elem.width = CurveWidthChange.Evaluate(elem.now / TimeChange);
                        elem.width = Mathf.Max(0.0f, elem.width);
                        elem.colour = GradientColor.Evaluate(elem.now / TimeChange);
                    }
                    else if (elem.now <= TimeChange)
                    {
                        elem.now += Time.fixedDeltaTime;
                        elem.width = CurveWidthChange.Evaluate(1.0f);
                        elem.width = Mathf.Max(0.0f, elem.width);
                        elem.colour = GradientColor.Evaluate(1.0f);
                    }
                    if (e == seg.tail)
                        break;
                }
            }
        }

        private void ResetElement()
        {
            Vector3 position = transform.position;
            mPreTexcoord = 0;
            Color cc = InitialColor;
            Element e = new Element(position, InitialWidth, mPreTexcoord, cc);
            AddChainElement(0, e);

            mPreTexcoord += mElemLength;
            Element e1 = new Element(position, InitialWidth, mPreTexcoord, cc);
            AddChainElement(0, e1);
        }

        private void SetInitialData()
        {
            InitialColor = GradientColor.Evaluate(0.0f);
            InitialWidth = CurveWidthChange.Evaluate(0.0f);
        }

        private void CreateLine()
        {
            Vector3 point1 = transform.GetChild(0).position;
            Vector3 point2 = transform.GetChild(1).position;

        }

        private void SetLineHelper()
        {

            PointHelper0 = GameObject.Instantiate(pointHelperPrefab, Point_0 + transform.position, transform.rotation, transform);
            PointHelper1 = GameObject.Instantiate(pointHelperPrefab, Point_1 + transform.position, transform.rotation, transform);
            LineEmptyObject = new GameObject();
            LineEmptyObject.name = "LineHelper";

            PointHelper0.transform.localPosition = GetHelperPosition(PointHelper0.transform.localPosition);
            PointHelper1.transform.localPosition = GetHelperPosition(PointHelper1.transform.localPosition);
            LineEmptyObject.AddComponent<LineRenderer>();


            mLineRenderer = LineEmptyObject.GetComponent<LineRenderer>();
            mLineRenderer.material = LineMaterial;
            mLineRenderer.positionCount = 2;
            mLineRenderer.startColor = Color.white;
            mLineRenderer.endColor = Color.white;
            mLineRenderer.startWidth = 0.004f;
            mLineRenderer.endWidth = 0.004f;

            mLineRenderer.useWorldSpace = true;
            mLineRenderer.SetPosition(0, PointHelper0.transform.position);
            mLineRenderer.SetPosition(1, PointHelper1.transform.position);
        }

        private Vector3 GetHelperPosition(Vector3 position)
        {
            Vector3 point = new Vector3(position.x * transform.localScale.x,
                                        position.y * transform.localScale.y,
                                        position.z * transform.localScale.z);
            return point;
        }

        private void DestroyLineHelper()
        {
            DestroyImmediate(PointHelper0);
            DestroyImmediate(PointHelper1);
            DestroyImmediate(LineEmptyObject);

            mLineRenderer = null;
        }

        private void UpdateLineHelper()
        {
            mLineRenderer.SetPosition(0, PointHelper0.transform.position);
            mLineRenderer.SetPosition(1, PointHelper1.transform.position);
            mVecTrailBoneChain[0].UpdateBonePos(PointHelper0.transform.localPosition, PointHelper1.transform.localPosition);
            Point_0 = PointHelper0.transform.localPosition;
            Point_1 = PointHelper1.transform.localPosition;

        }

        private int GetLength(ChainSegment ChainSeg)
        {
            if (ChainSeg.head <= ChainSeg.tail)
                return ChainSeg.tail - ChainSeg.head + 1;
            return ChainSeg.tail + (MaxChainElements - ChainSeg.head) + 1;
        }

        private int GetIndex(ChainSegment chainSeg, float percentFromTail, ref float fPercentToLast)
        {
            float fLength = (GetLength(chainSeg) - 1) * percentFromTail;
            int nLast = (int)(fLength);
            fPercentToLast = fLength - nLast;
            nLast = chainSeg.tail - nLast;
            if (nLast < 0)
            {
                nLast = MaxChainElements + nLast;
            }
            return nLast;
        }

        private void MaxSegmentChange()
        {
            if (MaxSegments < MaxChainElements)
                MaxSegments = MaxChainElements;

            if (MaxSegments > mMaxSegmentsRecord)
            {
                int vertexAddNum = (MaxSegments + 1) * 3 - (mMaxSegmentsRecord + 1) * 3;
                int idxAddNum = (MaxSegments + 1) * 6 * 2 - (mMaxSegmentsRecord + 1) * 6 * 2;

                Vertex[] vertex = new Vertex[vertexAddNum];
                for (int i = 0; i < vertexAddNum; ++i)
                {
                    vertex[i].position = transform.position;
                    vertex[i].uv = new Vector2(0, 0);
                    vertex[i].uv1 = new Vector2(0, 0);
                    vertex[i].color = Color.clear;
                }
                mVertexData.AddRange(vertex);

                int[] idx = new int[idxAddNum];
                mIndexBuffer.AddRange(idx);

            }
            else if (MaxSegments < mMaxSegmentsRecord)
            {
                int vertexRemoveStartIdx = (MaxSegments + 1) * 3;
                int idxBufferRemoveStartIdx = (MaxSegments + 1) * 6 * 2;
                mVertexData.RemoveRange(vertexRemoveStartIdx, mVertexData.Count - vertexRemoveStartIdx);
                mIndexBuffer.RemoveRange(idxBufferRemoveStartIdx, mIndexBuffer.Count - idxBufferRemoveStartIdx);
            }
            mMaxSegmentsRecord = MaxSegments;
            mIndexNeedChange = true;
        }

        private void SetUpBuffers()
        {
            mVertexData.Clear();
            mIndexBuffer.Clear();
            if (BillboardType <= 1)
            {
                Vertex[] vertex = new Vertex[MaxChainElements * 2];
                for (int i = 0; i < MaxChainElements * 2; ++i)
                {
                    vertex[i].position = transform.position;
                    vertex[i].uv = new Vector2(0, 0);
                    vertex[i].uv1 = new Vector2(0, 0);
                    vertex[i].color = Color.clear;
                }
                mVertexData.AddRange(vertex);


                int[] idx = new int[MaxChainElements * 6];
                mIndexBuffer.AddRange(idx);
            }
            else
            {
                Vertex[] vertex = new Vertex[(MaxSegments + 1) * 3];
                for (int i = 0; i < (MaxSegments + 1) * 3; ++i)
                {
                    vertex[i].position = transform.position;
                    vertex[i].uv = new Vector2(0, 0);
                    vertex[i].uv1 = new Vector2(0, 0);
                    vertex[i].color = Color.clear;
                }
                mVertexData.AddRange(vertex);


                int[] idx = new int[(MaxSegments + 1) * 6 * 2];
                mIndexBuffer.AddRange(idx);
            }
        }

        private void CleanHelper()
        {
            for (int i = 0; i < 2; i++)
            {
                if (i == 0)
                {
                    GameObject lineHelper = GameObject.Find("LineHelper");
                    if (lineHelper != null)
                    {
                        DestroyImmediate(lineHelper);
                    }
                }
                GameObject pointHelper = GameObject.Find("PointHelper(Clone)");
                if (pointHelper != null)
                    DestroyImmediate(pointHelper);
            }
        }
    }
}
