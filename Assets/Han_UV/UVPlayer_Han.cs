using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;
using FreeImageAPI;
using System.Threading;
using System;
using System.Runtime.InteropServices;

public class UVPlayer_Han : MonoBehaviour
{
    [Header("UV�ļ�������·��")]
    public string folderPath = null;
    private List<byte[]> imgs_byte = new List<byte[]>();
    private byte[] black_byte = null;

    [Header("��Ƶ�������")]
    public VideoPlayer player = null;
    [Header("������CS�ļ�")]
    public ComputeShader player_cs = null;
    private ComputeBuffer buffer_cs = null;

    [Header("��ƵRT")]
    public RenderTexture video_rt = null;
    [Header("���պϳ�RT")]
    private RenderTexture out_rt = null;
    [Header("������Ƭ")]
    public Texture2D photo = null;

    [Header("Canvas-���ճ����RawImage")]
    public RawImage raw = null;
    [Header("Canvas-�������")]
    public Text text_debug = null;
    [Header("UV���ֵ���ʼλ��")]
    public int start_index = 57;
    private int load_index = 0;

    private long old_frame = -1;
   
    private bool is_load = false;
    private bool load_done = false;
    private Thread th_load = null;
    private FIBITMAP bitmap;

    // Start is called before the first frame update
    void Start()
    {
        //�������RT
        out_rt = new RenderTexture(video_rt.width, video_rt.height, 
            video_rt.depth, video_rt.format, RenderTextureReadWrite.Linear);
        out_rt.enableRandomWrite = true;
        out_rt.wrapMode = TextureWrapMode.Clamp;
        out_rt.filterMode = FilterMode.Trilinear;
        out_rt.Create();
        raw.texture = out_rt;
        //���߳̿�ʼ����ͼƬ����
        is_load = true;
        th_load = new Thread(LoadImgs);
        th_load.Start();
    }
    private void OnApplicationQuit()
    {
        CloseThread();
        imgs_byte.Clear();
        imgs_byte = null;
        GC.Collect();
    }

    private void Update()
    {
        text_debug.text = ($"load index: {load_index}");

        //����׼����
        if (load_done)
        {
            black_byte = new byte[imgs_byte[0].Length];
            buffer_cs = new ComputeBuffer(imgs_byte[0].Length / 4, 4);
            player.Play();
            load_done = false;
        }

        //������ÿ֡���д���
        if (player.frame != old_frame)
        {
            int index = (int)(player.frame - start_index);
            if (player.frame >= start_index && index < imgs_byte.Count)
            {
                buffer_cs.SetData(imgs_byte[index]);
            }
            else buffer_cs.SetData(black_byte);
            //buffer_cs.SetData(imgs_byte[0]);

            UVProcess();
            old_frame = player.frame;
        }
    }

    //����ʵʱ����
    public void UVProcess()
    {
        int k = player_cs.FindKernel("CSMain");

        player_cs.SetInt("width", out_rt.width);
        player_cs.SetInt("height", out_rt.height);
        player_cs.SetInt("photo_w", photo.width);
        player_cs.SetInt("photo_h", photo.height);
        player_cs.SetBuffer(k, "uv", buffer_cs);
        player_cs.SetTexture(k, "photo", photo);
        player_cs.SetTexture(k, "video", video_rt);
        player_cs.SetTexture(k, "output", out_rt);

        player_cs.Dispatch(k, out_rt.width / 8, out_rt.height / 8, 1);
    }
    //����ͼƬ
    private void LoadImgs()
    {
        string[] imageFiles = Directory.GetFiles(folderPath, "*.png");
        Debug.Log($"file len: {imageFiles.Length}");
        int total = imageFiles.Length;

        while (is_load)
        {
            bitmap = FreeImage.Load(FREE_IMAGE_FORMAT.FIF_PNG, imageFiles[load_index], FREE_IMAGE_LOAD_FLAGS.DEFAULT);
            uint width = FreeImage.GetWidth(bitmap);
            uint height = FreeImage.GetHeight(bitmap);
            byte[] imageData = new byte[width * height * 3 * 2];
            byte[] imageData2 = new byte[width * height * 2 * 2];
            IntPtr ptr = FreeImage.GetBits(bitmap);
            Marshal.Copy(ptr, imageData, 0, imageData.Length);
            for (int i = 0; i < imageData.Length / 6; i++)
            {
                imageData2[i * 4 + 0] = imageData[i * 6 + 0];
                imageData2[i * 4 + 1] = imageData[i * 6 + 1];
                imageData2[i * 4 + 2] = imageData[i * 6 + 2];
                imageData2[i * 4 + 3] = imageData[i * 6 + 3];
            }
            imgs_byte.Add(imageData2);

            FreeImage.Unload(bitmap);

            Debug.Log($"load index: {load_index}");
            load_index++;
            if(load_index >= total)
            {
                load_done = true;
                CloseThread();
            }
        }
    }
    //�ر��߳�
    private void CloseThread()
    {
        if (th_load != null)
        {
            is_load = false;
            th_load.Abort();
            th_load = null;
        }
    }

}
