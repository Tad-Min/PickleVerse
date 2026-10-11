using UnityEngine;
using UnityEngine.EventSystems;

namespace PickleVerse.Shop
{
    public sealed class PaddlePreview : MonoBehaviour,IDragHandler,IBeginDragHandler,IEndDragHandler,IScrollHandler
    {
        public Transform pivot;public Camera previewCamera;
        public GameObject[] models;
        public string[] modelIds=System.Array.Empty<string>();
        public bool autoRotate=true;
        public float yaw=-18,pitch=-8,zoom=1;
        Vector3 cameraStart;
        bool dragging;
        public int Selected {get;private set;}
        void Awake(){cameraStart=previewCamera.transform.localPosition;Apply();}
        public void UseCamera(Camera camera){previewCamera=camera;cameraStart=camera.transform.localPosition;Apply();}
        void Update(){if(autoRotate&&!dragging){yaw+=Time.unscaledDeltaTime*16;Apply();}}
        public void Select(int index){Selected=index;for(int i=0;i<models.Length;i++)models[i].SetActive(i==index);ResetView();}
        public void SelectOffer(PaddleOffer offer,int index,int count)
        {
            if(models.Length>count)for(int i=count;i<models.Length;i++)if(models[i])Destroy(models[i]);
            if(models.Length!=count)System.Array.Resize(ref models,count);
            if(modelIds.Length!=count)System.Array.Resize(ref modelIds,count);
            if(models[index]&&modelIds[index]!=offer.id){models[index].SetActive(false);Destroy(models[index]);models[index]=null;}
            if(!models[index])
            {
                models[index]=Instantiate(offer.previewPrefab,pivot);
                foreach(var t in models[index].GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            }
            modelIds[index]=offer.id;
            Selected=index;for(int i=0;i<models.Length;i++)if(models[i])models[i].SetActive(i==index);ResetView();
        }
        public void ResetView(){yaw=-18;pitch=-8;zoom=1;autoRotate=true;Apply();}
        public void Rotate(float horizontal,float vertical){yaw=Mathf.Repeat(yaw+horizontal,360);pitch=Mathf.Clamp(pitch+vertical,-65,65);Apply();}
        public void ZoomBy(float amount){zoom=Mathf.Clamp(zoom+amount,.42f,1.65f);Apply();}
        public void ZoomIn(){ZoomBy(-.12f);}public void ZoomOut(){ZoomBy(.12f);}
        void Apply(){pivot.localRotation=Quaternion.Euler(pitch,yaw,0);if(cameraStart!=Vector3.zero)previewCamera.transform.localPosition=cameraStart*zoom;}
        public void ToggleRotation(){autoRotate=!autoRotate;}
        public void OnBeginDrag(PointerEventData e){dragging=true;autoRotate=false;}
        public void OnDrag(PointerEventData e){Rotate(-e.delta.x*.42f,e.delta.y*.28f);}
        public void OnEndDrag(PointerEventData e){dragging=false;}
        public void OnScroll(PointerEventData e){ZoomBy(-e.scrollDelta.y*.07f);}
    }
}
