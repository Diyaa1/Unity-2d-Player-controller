
using UnityEngine;

public class Player2dController : MonoBehaviour
{

    #region  Core Structures



    struct RayCastOrigin
    {
        public Vector2 TopLeft;
        public Vector2 TopRight;
        public Vector2 BottomLeft;
        public Vector2 BottomRight;
    }

    struct CollisionStates
    {
        public bool above;
        public bool down;
        public bool left;
        public bool right;

        public bool wasGrounded;
        public bool becameGroundedThisFrame;
        public void Reset()
        {
            above = down = left = right = false;
        }
    }



    #endregion



    #region Public Fields



    [Range(1, 40)]
    public int numberOfHorizontalLines;


    [Range(1, 60)]
    public int numberOfVerticalLines;

    /// <summary>
    /// skinWidth is used so that we avoid zero length ray. so that the ray is always fired from inside player 
    /// Note : we always add this value to our ray length.
    /// Note : Remember to take skinWidth into account when you want to get the distance from rayOrigin
    /// and hit point so we move the player in the correct amount.
    /// </summary>
    [Range(0.001f, 0.3f)]
    public float skinWidth = 0.003f;


    /// <summary>
    /// the max slope angle that we can handle
    /// </summary>
    /// <value>The slope limit.</value>
    [Range(30f, 80f)]
    public float MaximumSlope;


    [Range(30f, 80f)]
    public float maximumDownwardSlopeAngle;


    public AnimationCurve speedModifier = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(80f, 0.0f));


    /// <summary>
	/// mask with all layers thats trigger ray hit
	/// </summary>
    public LayerMask hitMask;


    /// <summary>
	/// mask with all layers for one way platformer case
	/// </summary>
    public LayerMask oneWayPlatformerMask;

    public bool ignoreOneWayPlatforemer = false;

    public Vector2 velocity;

    #endregion



    #region Private Fields




    Rigidbody2D rigidBody2D;


    RayCastOrigin rayOrigins;


    CollisionStates collisioStates;


    Collider2D Collider2D;



    float rayHorizontalSpacing;


    float rayVerticalSpacing;

    RaycastHit2D hit;
    RaycastHit2D hit2;

    Vector2 initialDeltaMovement;

    

    #endregion



    #region  getters,setters,Property Access 



    public bool isGrounded()
    {
        return collisioStates.down;
    }
    public bool hittingAbove()
    {
        return collisioStates.above;
    }
    public bool wasGrounded()
    {
        return collisioStates.wasGrounded;
    }
    public bool becameGroundedThisFrame()
    {
        return collisioStates.becameGroundedThisFrame;
    }




    #endregion




    #region Initializing Methos



    /// <summary>
	/// Recalculate Ray origins should be called everytime we change skin width
	/// </summary>
    private void ReCalculateRayOrigins()
    {
        Bounds bounds = Collider2D.bounds;
        bounds.Expand(-1f * 2f * skinWidth);
        Vector2 min = bounds.min;
        Vector2 max = bounds.max;

        rayOrigins.BottomLeft = min;
        rayOrigins.TopRight = max;
        rayOrigins.BottomRight = new Vector2(max.x, min.y);
        rayOrigins.TopLeft = new Vector2(min.x, max.y);

        CalculateRaySpacing();
    }

    /// <summary>
	/// Calaculate ray spacing based on skin width based on number of rays and ray origins
	/// </summary>
    private void CalculateRaySpacing()
    {
        rayHorizontalSpacing = (rayOrigins.TopRight.x - rayOrigins.BottomLeft.x) / (numberOfVerticalLines - 1);
        rayVerticalSpacing = (rayOrigins.TopRight.y - rayOrigins.BottomLeft.y) / (numberOfHorizontalLines - 1);
    }


    #endregion



    #region Unity Mehods



    void Awake()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        Collider2D = GetComponent<Collider2D>();

        ReCalculateRayOrigins();

        ///TODO CC2d igonre other layers
    }
    #endregion



    #region  Public Methods

    /// <summary>
    /// Main public method takes delta movement vector then update player transform after checking all the movement.
    /// </summary>
    /// <param name="deltaMovement">Delta movement</param>
    public void move(Vector2 deltaMovement)
    {
        //do clean up and save some values
        FrameInitialization(deltaMovement);

        // first we handle downward slopes below us 
        // only check if we are grounded and theres a force thats want us to stick on ground
        if (wasGrounded() && deltaMovement.y < 0 )
        {
            handleDownWardSlopes(ref deltaMovement);
        }

        // We check horizontal movement
        if (deltaMovement.x != 0)
        {
            handleHorizontalMovement(ref deltaMovement);
        }
        // We check vertical movement
        if (deltaMovement.y != 0)
        {
            handleVerticalMovement(ref deltaMovement);
        }

        FrameEnd();

        transform.Translate(deltaMovement);





    }

    #endregion



    #region Private Methods




    /// <summary>
    /// Each Frame we recaculate ray origins based on new collider position .
    /// we use SyncTransforms so that our collider position change every update call not fixed update only.
    /// </summary>
    void FrameInitialization(Vector2 deltaMovement)
    {
        initialDeltaMovement = deltaMovement;
        collisioStates.wasGrounded = collisioStates.down;
        collisioStates.Reset();
        Physics2D.SyncTransforms();
        ReCalculateRayOrigins();

    }
    void FrameEnd()
    {

        if (collisioStates.down && !collisioStates.wasGrounded)
        {
            collisioStates.becameGroundedThisFrame = true;
        }


    }

    /// <summary>
    /// We fire a number of rays in horizontal direction each one with length is equal to deltaMovemnt.x
    /// , while accounting for skinWidth as we fire our ray from inside our player.
    /// for our first ray we call firstHorizontalRayCheck as we want to handle it differently (slopes , one way platformer slopes).
    /// any other case we will just stop the player from moving.
    /// </summary>
    private void handleHorizontalMovement(ref Vector2 deltaMovement)
    {

        var isGoingRight = deltaMovement.x > 0;
        var direction = isGoingRight ? Vector2.right : -Vector2.right;
        var initialRayOrigin = isGoingRight ? rayOrigins.BottomRight : rayOrigins.BottomLeft;
        var rayLength = isGoingRight ? deltaMovement.x + skinWidth : Mathf.Abs(deltaMovement.x - skinWidth);

        for (int i = 0; i < numberOfHorizontalLines; i++)
        {
            var rayOrigin = initialRayOrigin;
            rayOrigin.y += rayVerticalSpacing * i;

            
            LayerMask mask = i == 0 ? (int)hitMask | (int)oneWayPlatformerMask : (int)hitMask;
            if(ignoreOneWayPlatforemer) mask = mask & ~oneWayPlatformerMask;

            hit = Physics2D.Raycast(rayOrigin, direction, rayLength, mask);

            if (!hit) continue;

            if (i == 0)
            {
                firstHorizontalRayCheck(ref deltaMovement);
            }
            else
            {
                deltaMovement.x = 0;
            }

        }

    }

    /// <summary>
    /// here we check if our hit need to be handled as slope hit (filtring) then we call  handleHorizontalSlope.
    /// if our #procedure check fail we return without handling it which will allow player to pass as theres nothing in front of it.
    /// if our #procedure check pass we handle it as normal slope .
    /// </summary>
    private void firstHorizontalRayCheck(ref Vector2 deltaMovement)
    {
        var normal = hit.normal;

        // first check to see if we are hitting a one way platformer 
        if (((1 << hit.transform.gameObject.layer) & oneWayPlatformerMask.value) != 0)
        {
            //Check if we are hitting from the correct side by checking if the difference between the angle of object rotation and normal vector angle is near 90
            float ZRotationOfObject = hit.transform.eulerAngles.z;
            if (ZRotationOfObject > 180)
            {
                ZRotationOfObject -= 180;
            }
            var difference = Mathf.Abs(Vector2.Angle(Vector2.right, hit.normal) - ZRotationOfObject);

            //if angle is not near act as if the hit didn't happen.
            if (difference > 90.2f || difference < 89.8f)
            {
                return;
            }

        }

        if (handleHorizontalSlope(ref deltaMovement))
        {
            deltaMovement.x = 0;
        }

    }

    /// <summary>
    /// Handle  upward slopes 
    /// </summary>
    private bool handleHorizontalSlope(ref Vector2 deltaMovement)
    {
        var angle = Vector2.Angle(Vector2.up, hit.normal);
        Debug.Log(angle);
        if (angle > MaximumSlope)
        {
            return true;
        }

        deltaMovement *= speedModifier.Evaluate(angle);

        // calculate y using the tangent function in right triangles.
        float deltaY = Mathf.Tan(angle * Mathf.Deg2Rad) * Mathf.Abs(deltaMovement.x);

        // we check this because we are checking horizontal movement before vertical movement.
        // if we are jumping we dont handle stop
        var isPlayerJumping = Mathf.Sign(deltaMovement.y) == Mathf.Sign(deltaY) && Mathf.Abs(deltaMovement.y) > Mathf.Abs(deltaY);
        if (isPlayerJumping) return false;


        collisioStates.down = true;
        deltaMovement.y = deltaY;

        // Safety Check for example if we transition from low angle slope to higher one we fire ray from our calculated y.
        // We add our delta y so we are checking out next x,y position not next x only without taking y into account.
        var isGoingRight = deltaMovement.x > 0;
        var rayOrigin = isGoingRight ? rayOrigins.BottomRight : rayOrigins.BottomLeft;

        rayOrigin.y += deltaMovement.y;

        var direction = isGoingRight ? Vector2.right : -Vector2.right;
        var rayLength = isGoingRight ? deltaMovement.x + skinWidth : Mathf.Abs(deltaMovement.x - skinWidth);

        hit = Physics2D.Raycast(rayOrigin, direction, rayLength, 1 << 0);

        if (hit)
        {
            deltaMovement.x = hit.point.x - rayOrigin.x;
            deltaMovement.x += isGoingRight ? -skinWidth : skinWidth;
        }


        return false;
    }


    /// <summary>
    /// Handle Vertical Movement
    /// </summary>
    private void handleVerticalMovement(ref Vector2 deltaMovement)
    {
        var isGoingUp = deltaMovement.y > 0;
        var direction = isGoingUp ? Vector2.up : -Vector2.up;
        var initialRayOrigin = isGoingUp ? rayOrigins.TopRight : rayOrigins.BottomRight;

        var mask = isGoingUp || ignoreOneWayPlatforemer ? (int)hitMask : hitMask | oneWayPlatformerMask;
        
        for (int i = 0; i < numberOfVerticalLines; i++)
        {

            var rayLength = isGoingUp ? deltaMovement.y + skinWidth : Mathf.Abs(deltaMovement.y - skinWidth);
            var rayOrigin = initialRayOrigin;

            rayOrigin.x += deltaMovement.x;
            rayOrigin.x -= (rayHorizontalSpacing * i);

            hit = Physics2D.Raycast(rayOrigin, direction, rayLength, mask);
        
            if (hit)
            {

                if (((1 << hit.transform.gameObject.layer) & oneWayPlatformerMask.value) != 0 && hit.distance == 0)
                {
                    deltaMovement.y = initialDeltaMovement.y;
                    collisioStates.down = false;
                    return;
                }

                var amount = hit.point.y - rayOrigin.y;
                deltaMovement.y = amount + (isGoingUp ? -skinWidth : skinWidth);

                if (isGoingUp)
                    collisioStates.above = true;
                else
                {
                    collisioStates.down = true;
                }
                checkIfExtreamAngle();
            }
        }

        //Safety Check if we didnt detect jagged corners

        if (deltaMovement.y < 0)
        {
            var checkDirection = Vector2.right;
            var checkRayOrigin = rayOrigins.BottomLeft;
            var checkRayLength = rayOrigins.BottomRight.x - rayOrigins.BottomLeft.x;

            checkRayOrigin.x += deltaMovement.x;
            checkRayOrigin.y += (deltaMovement.y);


            hit = Physics2D.Raycast(checkRayOrigin, checkDirection, checkRayLength, mask);

            checkDirection *= -1;
            checkRayOrigin = rayOrigins.BottomRight;
            checkRayOrigin += deltaMovement;

            hit2 = Physics2D.Raycast(checkRayOrigin, checkDirection, checkRayLength, mask);

            if (hit && hit2)
            {
                if (Mathf.Sign(hit.normal.y) == Mathf.Sign(hit2.normal.y))
                {   
                    if(hit.normal.y > 0){
                        deltaMovement.y = 0;
                    }
                }
            }
        }

    }

    private void checkIfExtreamAngle(){
        var angle = Vector2.Angle(hit.normal,Vector2.up);
        Debug.Log(angle);
        if(angle > MaximumSlope){


            collisioStates.down =false;
        }
    }
    /// <summary>
    /// Handle  downward slopes   
    /// </summary>
    private void handleDownWardSlopes(ref Vector2 deltaMovement)
    {

        var isGoingRight = deltaMovement.x > 0;
        var direction = Vector2.down;
        var initialRayOrigin = isGoingRight ? rayOrigins.BottomLeft : rayOrigins.BottomRight;
        initialRayOrigin.x += deltaMovement.x;
        var rayLength = 0.1f; /* arbitrary value */

        var mask =  ignoreOneWayPlatforemer ? (int)hitMask : hitMask | oneWayPlatformerMask;
        

        hit = Physics2D.Raycast(initialRayOrigin, direction, rayLength, mask);


        if (hit)
        {
            var angle = Vector2.Angle(Vector2.up, hit.normal);
            if (angle > maximumDownwardSlopeAngle)
            {
                return;
            }
            /* are we going downward slope ? */
            var goingDownwardSlope = (Mathf.Sign(deltaMovement.x) == Mathf.Sign(hit.normal.x));

            if (goingDownwardSlope)
            {
                deltaMovement.y = (hit.point.y - initialRayOrigin.y) - skinWidth;
            }
        }
    }



    #endregion

}





























// private void handleDownWardSlopes(ref Vector2 deltaMovement){
//     /* Fire a ray from center based on calculated length based  on maxAngel to check if theres ground if yes we set delta y to any value to force ground check */
//     var direction = Vector2.down;  
//     var initialRayOrigin = new Vector2();
//     var colliderHalfWidth = (rayOrigins.BottomRight.x - rayOrigins.BottomLeft.x)/2;
//     initialRayOrigin.x = rayOrigins.BottomLeft.x + colliderHalfWidth; /* Get center of of collider */
//     initialRayOrigin.y = rayOrigins.BottomLeft.y; /* get base y */
//     /* get our down ray length based on our maximum angle  (tan0*x = y )  x is half width of collider*/
//     var rayLength = Mathf.Tan(maximumDownwardSlopeAngle * Mathf.Deg2Rad) * Mathf.Abs(colliderHalfWidth) +skinWidth;
//     /* fire ray */
//     hit = Physics2D.Raycast(initialRayOrigin,direction,rayLength ,1 << 0);

//     if(hit){
//         /* if angle is zero dont check */
//         var angle = Vector2.Angle(Vector2.up,hit.normal);
//         if(angle == 0){
//             return;
//         }

//         /* are we going downward slope ? */
//         var goingDownwardSlope = (Mathf.Sign(deltaMovement.x) == Mathf.Sign(hit.normal.x)); 


//         if(goingDownwardSlope){


//             /*here we check if we are close enough to slope we do that by calculating expected length this mean if theres a slope behind us with (x) angle
//                    this how far we would be from it so that one corner of collider would hit , we add a value as computer is'nt a god*/

//             /*Note :  we do this because we are firing our ray from center not collider corners */

//             var slopeAngle = Vector2.Angle(Vector2.up , hit.normal); /* angle of surface */

//             /*use surface angle to get expected length */
//             var expectedLength = Mathf.Tan(slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(colliderHalfWidth) +skinWidth;
//             if(Mathf.Abs(hit.distance - expectedLength) < 0.01f){  
//                 deltaMovement.y = (hit.point.y -initialRayOrigin.y) - skinWidth;
//             }
//         }

//     }


// }