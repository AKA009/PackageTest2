using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace S3Unity.STrail
{
    public class DoubleVector3
    {
        public Vector3 m_pos0;
        public Vector3 m_pos1;
        public Vector3 m_vInitHalf;
        public Vector3 m_vInitCenter;
        public float m_fScale;
        public DoubleVector3()
        {
            m_pos0 = Vector3.zero;
            m_pos1 = Vector3.zero;
            m_vInitHalf = Vector3.zero;
            m_vInitCenter = Vector3.zero;
            m_fScale = 1.0f;
        }
        
        DoubleVector3(Vector3 v1,Vector3 v2)
	    {
		    m_pos0=v1;
		    m_pos1=v2;
		    m_vInitCenter = (m_pos0+m_pos1)*0.5f;
		    m_vInitHalf = (m_pos1-m_pos0)*0.5f;
		    m_fScale = 1.0f;
	    }
        ~DoubleVector3()
        {

        }
        public float GetLength()
        {
            return (m_pos1 - m_pos0).magnitude;
        }

        public Vector3 GetHalf()
        {
            return m_vInitHalf * m_fScale;
        }
    }
    public class CTrailBoneChain
    {
        List<Vector3> m_vecTrailBone;
        List<DoubleVector3> m_vecPosSlot;
        Vector3 m_vCenter;
        Vector3 m_vHalf;
        float m_fBoneLength;

        int tail;
        int head;
        int m_nSegTail;

        public CTrailBoneChain(int PosSlotNum,Vector3 helper1,Vector3 helper2)
        {
            m_vecTrailBone = new List<Vector3>();
            m_vecTrailBone.Add(helper1);
            m_vecTrailBone.Add(helper2);
            UpdateCenterHalf();

             m_vecPosSlot = new List<DoubleVector3>();
            DoubleVector3[] initDoubleVector3 = new DoubleVector3[PosSlotNum];
            for(int i=0;i<PosSlotNum;++i)
            {
                initDoubleVector3[i] = new DoubleVector3();
            }
            m_vecPosSlot.AddRange(initDoubleVector3);
            m_nSegTail = 0;
            head = tail = 0;
        }

        public void AddTrailBone(Vector3 pTrailBone)
        {
            m_vecTrailBone.Add(pTrailBone);
        }

        public void UpdatePos(int index, Vector3 parentPos,Vector3 parentScale,Quaternion parentRotation)
        {
            Vector3 offset0 = new Vector3(m_vecTrailBone[0].x * parentScale.x,
                                        m_vecTrailBone[0].y * parentScale.y,
                                        m_vecTrailBone[0].z * parentScale.z);
            Vector3 offset1 = new Vector3(m_vecTrailBone[1].x * parentScale.x,
                                        m_vecTrailBone[1].y * parentScale.y,
                                        m_vecTrailBone[1].z * parentScale.z);
            /*Vector3 offset0 = m_vecTrailBone[0];
            Vector3 offset1 = m_vecTrailBone[1];*/

            m_vecPosSlot[index].m_pos0 = parentPos + parentRotation * offset0;
            m_vecPosSlot[index].m_pos1 = parentPos + parentRotation * offset1;

            m_vecPosSlot[index].m_vInitCenter = (m_vecPosSlot[index].m_pos0 + m_vecPosSlot[index].m_pos1) * 0.5f;
            m_vecPosSlot[index].m_vInitHalf = (m_vecPosSlot[index].m_pos1 - m_vecPosSlot[index].m_pos0) * 0.5f;

            m_vecPosSlot[index].m_pos0 = m_vecPosSlot[index].m_vInitCenter - m_vecPosSlot[index].m_vInitHalf * m_vecPosSlot[index].m_fScale;
            m_vecPosSlot[index].m_pos1 = m_vecPosSlot[index].m_vInitCenter + m_vecPosSlot[index].m_vInitHalf * m_vecPosSlot[index].m_fScale;
        }
        
        public void ScaleTrailWidth(int pos, float fScale)
        {
            m_vecPosSlot[pos].m_pos0 = m_vecPosSlot[pos].m_vInitCenter - m_vecPosSlot[pos].m_vInitHalf * fScale;
            m_vecPosSlot[pos].m_pos1 = m_vecPosSlot[pos].m_vInitCenter + m_vecPosSlot[pos].m_vInitHalf * fScale;
            m_vecPosSlot[pos].m_fScale = fScale;
        }

        public void UpdateTailPos(int uTail, int uTailLast, float percent)
        {
            if (m_vecPosSlot.Count < 2)
                return;
            DoubleVector3 DVectorTrail = m_vecPosSlot[uTail];

            DoubleVector3 DVectorTrailLast = m_vecPosSlot[uTailLast];
            DVectorTrail.m_pos0 = DVectorTrailLast.m_pos0 + (DVectorTrail.m_pos0 - DVectorTrailLast.m_pos0) * percent;
            DVectorTrail.m_pos1 = DVectorTrailLast.m_pos1 + (DVectorTrail.m_pos1 - DVectorTrailLast.m_pos1) * percent;
        }

        public int GetLastIndex(int index)
        {
            if (index == tail)
                return index;
            if (index == m_vecPosSlot.Count - 1)
                return 0;
            return index + 1;
        }

        public int GetNextIndex(int index)
        {
            if (index == head)
                return index;
            if (index == 0)
                return m_vecPosSlot.Count - 1;
            return index - 1;
        }

        public Vector3 CatmulRom(Vector3 T0, Vector3 P0, Vector3 P1, Vector3 T1, float f)
        {
            double DT1 = -0.5;
            double DT2 = 1.5;
            double DT3 = -1.5;
            double DT4 = 0.5;

            double DE2 = -2.5;
            double DE3 = 2;
            double DE4 = -0.5;

            double DV1 = -0.5;
            double DV3 = 0.5;

            double FAX = DT1 * T0.x + DT2 * P0.x + DT3 * P1.x + DT4 * T1.x;
            double FBX = T0.x + DE2 * P0.x + DE3 * P1.x + DE4 * T1.x;
            double FCX = DV1 * T0.x + DV3 * P1.x;
            double FDX = P0.x;

            double FAY = DT1 * T0.y + DT2 * P0.y + DT3 * P1.y + DT4 * T1.y;
            double FBY = T0.y + DE2 * P0.y + DE3 * P1.y + DE4 * T1.y;
            double FCY = DV1 * T0.y + DV3 * P1.y;
            double FDY = P0.y;

            double FAZ = DT1 * T0.z + DT2 * P0.z + DT3 * P1.z + DT4 * T1.z;
            double FBZ = T0.z + DE2 * P0.z + DE3 * P1.z + DE4 * T1.z;
            double FCZ = DV1 * T0.z + DV3 * P1.z;
            double FDZ = P0.z;

            float FX = (float)(((FAX * f + FBX) * f + FCX) * f + FDX);
            float FY = (float)(((FAY * f + FBY) * f + FCY) * f + FDY);
            float FZ = (float)(((FAZ * f + FBZ) * f + FCZ) * f + FDZ);

            return new Vector3(FX, FY, FZ);
        }

        public void GetSegmentPosAndDir(int index, float fPercent,ref Vector3 pos,ref Vector3 halfDir )
        {
            int indexNext = GetNextIndex(index);

            pos = CatmulRom(m_vecPosSlot[GetLastIndex(index)].m_vInitCenter, m_vecPosSlot[index].m_vInitCenter, m_vecPosSlot[indexNext].m_vInitCenter, m_vecPosSlot[GetNextIndex(indexNext)].m_vInitCenter, fPercent);
            halfDir = CatmulRom(m_vecPosSlot[GetLastIndex(index)].GetHalf(), m_vecPosSlot[index].GetHalf(), m_vecPosSlot[indexNext].GetHalf(), m_vecPosSlot[GetNextIndex(indexNext)].GetHalf(), fPercent);// *m_vecPosSlot[index].m_fScale;
        }

        public void SetSegTail(int n)
        {
            m_nSegTail = n <= 0 ? 0 : n;
        }

        void SetChainLength(int length)
        {
            DoubleVector3[] dou = new DoubleVector3[length];
            m_vecPosSlot.AddRange(dou);
        }
        
        public int GetSegTail()
        {
            return m_nSegTail;
        }
        
        public void SetHeadAndTail(int head_, int tail_)
        {
            head = head_;
            tail = tail_;
        }

        public float GetLength()
        {
            return m_fBoneLength;
        }

        public void UpdateBonePos(Vector3 pos1,Vector3 pos2)
        {
            m_vecTrailBone.Clear();
            m_vecTrailBone.Add(pos1);
            m_vecTrailBone.Add(pos2);
            UpdateCenterHalf();
        }

        public void UpdateCenterHalf()
        {
            if (m_vecTrailBone.Count != 2)
            {
                return;
            }
            m_vCenter = (m_vecTrailBone[0] + m_vecTrailBone[1]) * 0.5f;
            m_vHalf = (m_vecTrailBone[1] - m_vecTrailBone[0]) * 0.5f;
            m_fBoneLength = (m_vecTrailBone[0] - m_vecTrailBone[1]).magnitude;
        }
    }

}
