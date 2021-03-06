using UnityEngine;
using UnityEngine.InputSystem;


public class Chisel : MonoBehaviour
{
    public LineRenderer line;
    public Material foundCellMaterial;
    public Material pointerMaterial;
    public GameObject SecondaryController;
    public GameObject targetPrimitive;
    
    public InputActionProperty chiselAction;
    public InputActionProperty rotateAction;
    public InputActionProperty resetPRISMAction;

    public MarchingCubes potInScene;
    private Vector3Int targetCell;

    // Variables for PRISM
    public GameObject PrimaryHand;
    public GameObject SecondaryHand;
    
    private Vector3 lastPrimaryHandPos;
    private Vector3 lastSecondaryHandPos;

    private Vector3 lastPrimaryControllerPos;
    private Vector3 lastSecondaryControllerPos;

    // Start is called before the first frame update
    void Start()
    {
        targetCell = new Vector3Int(-1, -1, -1);
        chiselAction.action.canceled += ChiselPot;
        rotateAction.action.performed += RotatePot;
        resetPRISMAction.action.performed += ResetPRISM;

        targetPrimitive.GetComponent<MeshRenderer>().enabled = false;
        
        lastPrimaryHandPos = PrimaryHand.transform.position;
        lastSecondaryHandPos = SecondaryHand.transform.position;

        lastPrimaryControllerPos = this.transform.position;
        lastSecondaryControllerPos = SecondaryController.transform.position;
    }

    private void OnDestroy()
    {
        chiselAction.action.canceled -= ChiselPot;
        rotateAction.action.performed -= RotatePot;

        resetPRISMAction.action.performed -= ResetPRISM;
    }

    // Update is called once per frame
    void Update()
    {
        ApplyPRISM();
        // get direction from this hand to the non-dominant hand
        Vector3 toOther = SecondaryController.transform.position - this.transform.position;
        line.SetPosition(0, PrimaryHand.transform.InverseTransformPoint(this.transform.position));
        line.SetPosition(1, PrimaryHand.transform.InverseTransformPoint(SecondaryController.transform.position));
        line.material = pointerMaterial;
        // we need to find both the entry and exit point into the clay for our find nearest cell that contains clay method
        // In Unity, we must perform 2 raycasts in order to get the entry and exit points
        // Start by raycasting forward
        RaycastHit start;
        bool foundHit = Physics.Raycast(SecondaryController.transform.position, toOther, out start);
        if(foundHit)
        {
            line.SetPosition(1, PrimaryHand.transform.InverseTransformPoint(start.point));
        }
        if(!foundHit || start.collider.GetComponent<MarchingCubes>() == null)
        {
            // nothing to do; we didn't hit the pot first
            targetCell = new Vector3Int(-1, -1, -1);
            targetPrimitive.GetComponent<MeshRenderer>().enabled = false;
            return;
        }
        potInScene = start.collider.GetComponent<MarchingCubes>();
        // now that we found the entry point, we need the exit point. To find the exit, we are going to raycast
        // from the opposite direction. To be safe, we are going to to RaycastAll to check for every object
        // that collides with our ray, then scan to find the pot. This way, if there is an object immediately behind
        // the pot (say, the floor), we can still hit the pot to find the exit point.
        // Remember to shoot backwards from the goal to find our hit
        // 100 meters should be safe for our purposes; the scene isn't that large, and it isn't too large that we have
        // to worry about overflowing
        RaycastHit[] hits = Physics.RaycastAll(SecondaryController.transform.position + toOther * 100, -toOther);
        RaycastHit end = new RaycastHit();
        
        if(hits.Length == 0)
        {
            // then there is no point in continuing, because we didn't hit the pot somehow
            targetCell = new Vector3Int(-1, -1, -1);
            targetPrimitive.GetComponent<MeshRenderer>().enabled = false;
            return;
        }
        // since we entered the cube region, we are guarenteed an exit point, so find that as well
        foreach(RaycastHit objectIntersection in hits)
        {
            if(objectIntersection.collider.GetComponent<MarchingCubes>())
            {
                end = objectIntersection;
                break;
            }
        }
        // Find the grid cell point with clay in it that we hit
        targetCell = potInScene.FindClosestFilledCell(start, end);
        if (targetCell.x != -1 && targetCell.y != -1 && targetCell.z != -1)
        {
            Vector3 endPos = potInScene.CubeCellToWorldSpace(targetCell);
            line.SetPosition(1, PrimaryHand.transform.InverseTransformPoint(endPos));
            line.material = foundCellMaterial;
            targetPrimitive.GetComponent<MeshRenderer>().enabled = true;
            targetPrimitive.transform.position = endPos;
        }
    }
    
    void ApplyPRISM()
    {
        Vector3 curPrimaryHandPos = PrimaryHand.transform.position;
        Vector3 curSecondaryHandPos = SecondaryHand.transform.position;

        Vector3 primaryTravelDir = curPrimaryHandPos - lastPrimaryHandPos;
        Vector3 secondaryTravelDir = curSecondaryHandPos - lastSecondaryHandPos;
        
        float primaryTravelDist = primaryTravelDir.magnitude;
        float secondaryTravelDist = secondaryTravelDir.magnitude;
        
        float primaryScaleFactor = 1.0f;
        float secondaryScaleFactor = 1.0f;

        if (primaryTravelDist < .00065f)
        {
            primaryScaleFactor = 0;
        }
        else if(primaryTravelDist < .0015f)
        {
            float t = (primaryTravelDist - .00065f) / (.0015f - .00065f);
            primaryScaleFactor = Mathf.Lerp(.75f, 1.0f, t);
            //primaryScaleFactor = .75f;
        }
        else if(primaryTravelDist < .003f)
        {
            float normalized = (primaryTravelDist - .001f) / .001f;
            primaryScaleFactor = Mathf.Lerp(.2f, 1.0f, normalized);
            primaryScaleFactor = 1.0f;
        }
        else
        {
            primaryScaleFactor = 1.05f;
        }
        if (secondaryTravelDist < .00065f)
        {
            secondaryScaleFactor = 0;
        }
        else if (secondaryTravelDist < .0015f)
        {
            float t = (secondaryTravelDist - .00065f) / (.0015f - .00065f);
            secondaryScaleFactor = Mathf.Lerp(.75f, 1.0f, t);
            //secondaryScaleFactor = .75f;
        }
        else if(secondaryTravelDist < .003f)
        {
            secondaryScaleFactor = 1.0f;
        }
        else
        {
            secondaryScaleFactor = 1.05f;
        }
        Vector3 PRISMPrimaryPos = lastPrimaryControllerPos + primaryScaleFactor * primaryTravelDir;
        Vector3 PRISMSecondaryPos = lastSecondaryControllerPos + secondaryScaleFactor * secondaryTravelDir;

        lastPrimaryControllerPos = PRISMPrimaryPos;
        lastSecondaryControllerPos = PRISMSecondaryPos;

        this.transform.position = PRISMPrimaryPos;
        SecondaryController.transform.position = PRISMSecondaryPos;

        lastPrimaryHandPos = curPrimaryHandPos;
        lastSecondaryHandPos = curSecondaryHandPos;
    }

    void ChiselPot(InputAction.CallbackContext context)
    {
        if(targetCell.x != -1 && targetCell.y != -1 && targetCell.z != -1)
        {
            float curVal = potInScene.GetCell(targetCell);
            float newVal = Mathf.Max(0, curVal - 0.1f);
            potInScene.ChangeCell(targetCell, newVal);
            targetCell = new Vector3Int(-1, -1, -1);
            targetPrimitive.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    void RotatePot(InputAction.CallbackContext context)
    {
        Vector2 dir = context.ReadValue<Vector2>();
        float theta = dir.x * 4.0f;
        float curAngle = potInScene.GetRotation();
        potInScene.SetRotation(theta + curAngle);
    }

    private void OnDisable()
    {
        line.enabled = false;
        // Disable the PRISM as well
        this.gameObject.transform.position = PrimaryHand.transform.position;
        SecondaryController.transform.position = SecondaryHand.transform.position;
    }

    private void OnEnable()
    {
        line.enabled = true;
        this.gameObject.transform.position = PrimaryHand.transform.position;
        SecondaryController.transform.position = SecondaryHand.transform.position;
        lastPrimaryHandPos = PrimaryHand.transform.position;
        lastSecondaryHandPos = SecondaryHand.transform.position;
        lastPrimaryControllerPos = lastPrimaryHandPos;
        lastSecondaryControllerPos = lastSecondaryHandPos;
    }

    private void ResetPRISM(InputAction.CallbackContext context)
    {
        this.gameObject.transform.position = PrimaryHand.transform.position;
        SecondaryController.transform.position = SecondaryHand.transform.position;
        lastPrimaryHandPos = PrimaryHand.transform.position;
        lastSecondaryHandPos = SecondaryHand.transform.position;
        lastPrimaryControllerPos = lastPrimaryHandPos;
        lastSecondaryControllerPos = lastSecondaryHandPos;
    }
}
