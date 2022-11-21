#ifndef UNITY_INPUT_INCLUDED
#define UNITY_INPUT_INCLUDED

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixIV;
float4x4 unity_PrevMatrixM;
float4x4 unity_PrevMatrixIM;
float4x4 glstate_matrix_projection;
float3 _WorldSpaceCameraPos;

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;
CBUFFER_END
#endif
