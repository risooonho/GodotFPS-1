using Godot;
using Godot.Collections;
//using System;
public sealed class Player : KinematicBody, ISave
{
    private Player() { }
    private static Player _singleton;
    public static Player Singleton
    {
        get { return _singleton; }
        private set
        {
            SI.SetAndWarnUser(ref value, ref _singleton);
        }
    }

    public bool Imobile;
    private int Hinput;
    private int Vinput;
    private int Linput;
    private bool[] InputDoubleChecker = new bool[6];
    private Vector2 CameraAngle;

    [Export]
    public bool InvertX = false;
    [Export]
    public bool InvertY = false;
    [Export]
    public float SensitivityX = 0.3f;
    [Export]
    public float SensitivityY = 0.3f;
    [Export]
    public int PitchMin = -89;
    [Export]
    public int PitchMax = 89;

    [Export]
    public float FullSpeedFly = 1f;
    [Export]
    public float AccelerationFly = 4f;

    [Export]
    public float Gravity = -18.16f;
    [Export]
    public float FullSpeedWalk = 10f;
    [Export]
    public float FullSpeedSprint = 20f;
    [Export]
    public float AccelerationWalk = 4f;
    [Export]
    public float DeaccelerationWalk = 6f;
    [Export]
    public float AccelerationAir = 2f;
    [Export]
    public float DeaccelerationAir = 1f;

    [Export]
    public float ClimbSpeed = 0.2f;

    [Export]
    public float JumpHeight = 9f;
    [Export]
    public int JumpCount = 1;
    public int JumpsLeft = 0;
    [Export]
    public bool BunnyHopping = false;
    [Export]
    public bool CanCrouchJump = true;
    public bool CrouchJumped;

    [Export]
    public float HeadDipThreshold = -9f;
    [Export]
    public float FatalFallVelocity = 50f;
    [Export]
    public float MajorInjuryFallVelocity = 25f;
    [Export]
    public float MinorInjuryFallVelocity = 15f;

    [Export]
    private bool _headBobbing;
    public bool HeadBobbing
    {
        get{return _headBobbing;}
        set
        {
            if(!value) PlayerAnimationTree.Singleton.HeadBobBlend = 0f;
            _headBobbing = value;
        }
    }
    private readonly float BobIncrementWalk = 2f;
    private readonly float BobIncrementSprint = 3f;
    private readonly float BobDecrementFloor = 4f;
    private readonly float BobDecrementAir = 5f;
    Vector2 SwingTarget;

    [Export]
    public float MaxSlopeSlip = 30f;
    [Export]
    public float MaxSlopeNoWalk = 59f;

    public bool Flying = false;
    public bool Climbing = false;
    public Vector3 Velocity;
    public float ImpactVelocity;
    public Vector3 Direction;

    private bool WasInAir = false;

    private bool _crouched;
    public bool Crouched
    {
        get{return _crouched;} 
        set
        {
            if(value)
            {
                PlayerAnimationTree.Singleton.StateMachineCrouch.Travel("Crouch");
            }
            else
            {
                PlayerAnimationTree.Singleton.StateMachineCrouch.Travel("Uncrouch");
            }
            //Enable correct collision shape
            _CollisionStand.Disabled = value;
            _CollisionCrouch.Disabled = !value;

            //Enable/Disable StandArea
            StandArea.Monitoring = value;
            _crouched = value;
        }
    }
    private bool WantsToUncrouch = false;
    
    [Export]
    public bool CrouchToggle;
    
    [Export]
    public bool SprintToggle;
    [Export]
    public bool SprintToWalk;
    
    private bool Sprint;
    
    private CollisionShape _CollisionStand;
    private CollisionShape _CollisionCrouch;
    private Array<CollisionShape> OtherCollision;
    private float StandHeight;
    private float CrouchHeight;

    private Area StandArea;

    private Spatial _Head;
    private Camera _Camera;
	private Spatial ArmWrapper;
    private Timer _ClimbTimer;

    //private PlayerFeet _JumpArea;
    private SnapArea _SnapArea;

	private RayCast ArmRay;

    private RayCast GroundRay;
    private RayCast[] LedgeRays;
    private Label ClimbLabel;
    private Label JumpLabel;
    private Vector3 ClimbPoint;
    private Vector3 ClimbStep;
    private float ClimbDistance;

    public override void _EnterTree()
    {
        Singleton = this;
    }

    public override void _ExitTree()
    {
        Singleton = null;
    }

    public override void _Ready()
    {
        _CollisionStand = GetNode<CollisionShape>("CollisionStand");
        _CollisionCrouch = GetNode<CollisionShape>("CollisionCrouch");
        CapsuleShape shape1 = (CapsuleShape)_CollisionStand.Shape;
        CapsuleShape shape2 = (CapsuleShape)_CollisionCrouch.Shape;
        StandHeight = shape1.Height;
        CrouchHeight = shape2.Height;
        OtherCollision = NX.FindAll<CollisionShape>(this, true);
        OtherCollision.Remove(_CollisionStand);
        OtherCollision.Remove(_CollisionCrouch);

        StandArea = GetNode<Area>("StandArea");
        _Head = GetNode<Spatial>("Head");
        _Camera = GetNode<Camera>("Head/Wrapper1/Wrapper2/Wrapper3/Camera");
		ArmWrapper = GetNode<Spatial>("Head/Wrapper1/Wrapper2/Wrapper3/Camera/Viewport/Wrapper4");
        _ClimbTimer = GetNode<Timer>("ClimbTimer");
       
        //_JumpArea = GetNode<PlayerFeet>("JumpArea");
        _SnapArea = GetNode<SnapArea>("SnapArea");
        GroundRay = GetNode<RayCast>("GroundRay");
        LedgeRays = new RayCast[GetNode<Node>(("Head/LedgeRays")).GetChildCount()];
        for(int i = 0; i < LedgeRays.Length; ++i)
        {
            LedgeRays[i] = GetNode<RayCast>("Head/LedgeRays/LedgeRay" + (i + 1));
        }
        ClimbLabel = GetNode<Label>("UI/Reticle/ClimbLabel");
        JumpLabel = GetNode<Label>("UI/JumpLabel");
		
		ArmRay = GetNode<RayCast>("Head/Wrapper1/Wrapper2/Wrapper3/Camera/ArmRay");

        //Check for vital actions in InputMap
        IX.CheckAndAddAction("move_forward", KeyList.W);
        IX.CheckAndAddAction("move_backward", KeyList.S);
        IX.CheckAndAddAction("move_left", KeyList.A);
        IX.CheckAndAddAction("move_right", KeyList.D);
        IX.CheckAndAddAction("move_up", KeyList.E);
        IX.CheckAndAddAction("move_down", KeyList.Q);
        IX.CheckAndAddAction("toggle_fly", KeyList.V);
        IX.CheckAndAddAction("release_mouse", KeyList.F1, false, false, true);
        IX.CheckAndAddAction("place_debug_camera", KeyList.F2, false, false, true);

        MaxSlopeSlip = Mathf.Deg2Rad(MaxSlopeSlip);
        MaxSlopeNoWalk = Mathf.Deg2Rad(MaxSlopeNoWalk);

        //Set jumps left
        JumpsLeft = JumpCount;

        //Set animation
        HeadBobbing = _headBobbing;
        SwingTarget = Vector2.Zero;
        
        //Capture mouse
        Input.SetMouseMode(Input.MouseMode.Captured);

        StandArea.Monitoring = true;
    }

    public override void _PhysicsProcess(float delta)
    {
        GroundRay.Call("update", 0.1f + (-Velocity.y * delta));
		ArmWrapper.GlobalTransform = _Camera.GlobalTransform;
		PlayerArms.Singleton.Retract = ArmRay.IsColliding();
        if (!Imobile)
        {
            if (Flying)
            {
                Fly(delta);
            }
            else if (Climbing)
            {
                Climb(delta);
            }
            else
            {
                Walk(delta);
            }
        }
        if (Input.IsActionJustPressed("toggle_fly"))
        {
            Flying = !Flying;
            Velocity = Vector3.Zero;
            PlayerAnimationTree.Singleton.Active = !Flying;
        }
		
    }

    //Random random = new Random();

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey)
        {
            //Downcast to key
            InputEventKey eventKey = (InputEventKey)@event;

            //Update Motion Input
            if (!Imobile)
            {
                if (eventKey.IsAction("move_forward")) SetMotionParameterAndConsumeInput(ref Vinput, false, eventKey, 0);
                else if (eventKey.IsAction("move_backward")) SetMotionParameterAndConsumeInput(ref Vinput, true, eventKey, 1);
                else if (eventKey.IsAction("move_left")) SetMotionParameterAndConsumeInput(ref Hinput, false, eventKey, 2);
                else if (eventKey.IsAction("move_right")) SetMotionParameterAndConsumeInput(ref Hinput, true, eventKey, 3);
                else if (eventKey.IsAction("move_up")) SetMotionParameterAndConsumeInput(ref Linput, true, eventKey, 4);
                else if (eventKey.IsAction("move_down")) SetMotionParameterAndConsumeInput(ref Linput, false, eventKey, 5);
            }
            if (eventKey.IsAction("release_mouse") && eventKey.Pressed)
            {
                Input.SetMouseMode(Input.GetMouseMode() == Input.MouseMode.Captured ? Input.MouseMode.Visible : Input.MouseMode.Captured);
            }
        }
        else if (@event is InputEventMouseMotion && Input.GetMouseMode() == Input.MouseMode.Captured)
        {
            //Downcast to mouse motion
            InputEventMouseMotion eventMouseMotion = (InputEventMouseMotion)@event;

            //Update camera angle
            SwingTarget += eventMouseMotion.Relative.Normalized() * new Vector2(1, -1) * PlayerArms.SwingIncrement;
            SwingTarget = SwingTarget.Clamped(1f);
            CameraAngle.x = CameraAngle.x + eventMouseMotion.Relative.x * SensitivityX;
            CameraAngle.y = CameraAngle.y + eventMouseMotion.Relative.y * SensitivityY;

            //Apply rotation
            ApplyCameraAngle();
            
            //Tell event was handled
            GetTree().SetInputAsHandled();
        }
    }

    public void LookAt(Vector3 point)
    {
        Vector3 gt = _Camera.GetGlobalTransform().origin;
        Vector3 point2 = new Vector3(point.x, gt.y, point.z);
        float x = gt.z - point.z;
        float y = gt.y - point.y;
        float r1 = gt.DistanceTo(point2);
        float r2 = gt.DistanceTo(point);
        CameraAngle.x = Mathf.Rad2Deg(Mathf.Acos(x / r1));
        CameraAngle.y = Mathf.Rad2Deg(Mathf.Asin(y / r2));
        ApplyCameraAngle();
    }

    private void ApplyCameraAngle()
    {
        CameraAngle.y = Mathf.Clamp(CameraAngle.y, PitchMin, PitchMax);
        _Head.RotationDegrees = new Vector3(0, (InvertX ? 1 : -1) * CameraAngle.x, 0);
        _Camera.RotationDegrees = new Vector3((InvertY ? 1 : -1) * CameraAngle.y, 0, 0);
    }

    private void SetMotionParameterAndConsumeInput(ref int parameter, bool positive, InputEventKey inputEvent, int doubleCheckIndex)
    {
        GetTree().SetInputAsHandled();
        if (inputEvent.Echo) return;

        //Ensure player does not get stuck walking in one direction
        if (inputEvent.Pressed == InputDoubleChecker[doubleCheckIndex]) return;
        InputDoubleChecker[doubleCheckIndex] = inputEvent.Pressed;
        parameter += ((inputEvent.Pressed == positive) ? 1 : -1);

        //Tell Kevin he sucks and can't write working code
        if (parameter > 1 || parameter < -1)
            GD.Print("KEVIN YOUR INPUT IS STILL BROKEN!");
    }

    

    private void Fly(float delta)
    {
        //Reset direction
        Direction = Vector3.Zero;

        //Get the rotation of the head
        Basis aim = _Camera.GlobalTransform.basis;

        //Get input and set direction
        Direction += aim.z * Vinput;
        Direction += aim.x * Hinput;
        Direction += aim.y * Linput;
        Direction = Direction.Normalized();

        //Set target speed
        Vector3 target = Direction * FullSpeedFly;
        
        //Calculate velocity
        Velocity = Velocity.LinearInterpolate(target, AccelerationFly * delta);

        //Move Player
        //Translate(Velocity);
        GlobalTranslate(Velocity);
    }

    private void Walk(float delta)
    {
        //Since IsOnFloor() doesn't properly update on concave collision shapes, count the number of bodies that _JumpArea is colliding with
       

        //Get the rotation of the head
        Basis aim = _Head.GlobalTransform.basis;

        //Get slope of floor and if ground hit, set head velocity
        Vector3 normal;
        bool playerFeetOverlapsFloor = PlayerFeet.Singleton.Bodies > 0;
        if (playerFeetOverlapsFloor)
        {
            normal = GroundRay.GetCollisionNormal();
        }
        else
        {
            normal = Vector3.Up;
        }
        
        //Calculate ground angle
        float groundAngle = Mathf.Acos(normal.Dot(Vector3.Up));

        //Determine if slipping and set initial direction
        bool slipping = false;
        bool noWalkSlipping = false;
        if (groundAngle > MaxSlopeSlip)
        {
            slipping = true;
            if(groundAngle > MaxSlopeNoWalk)
            {
                noWalkSlipping = true;
            }
            Direction = Vector3.Up.Slide(normal);
        }
        else
        {
                Direction = Vector3.Zero;
        }

        //Get crouch
        bool crouchPressed = CrouchToggle && !CrouchJumped ? Input.IsActionJustPressed("crouch") : (Input.IsActionPressed("crouch") != Crouched);

        //Determine if landing
        PlayerArms.Singleton.Land = false;
        if(IsOnFloor()){
            if (WasInAir)
            {
                GD.Print("Landed");
                //Reset number of jumps
                JumpsLeft = JumpCount;
                JumpLabel.Text = "Jumps Left: " + JumpCount;

                //Play land animations
                PlayerArms.Singleton.Fall = false;
                PlayerArms.Singleton.Land = true;
                if (!slipping && ImpactVelocity >= HeadDipThreshold)
                {
                    PlayerAnimationTree.Singleton.LandBlend = Mathf.Min(1f, Mathf.Abs((ImpactVelocity - HeadDipThreshold) / (-Gravity - HeadDipThreshold)));
                    PlayerAnimationTree.Singleton.Land();
                }

                //Play Land Sounds if Velocity is significant
                PlayerFeet.Singleton.LandPlayer.Play();

                //GD.Print(ImpactVelocity);
                //Deal fall damage
                if (ImpactVelocity >= FatalFallVelocity)
                {
                    PlayerHealthManager.Singleton.Kill("A Fatal Fall");
                }
                else if(ImpactVelocity >= MajorInjuryFallVelocity)
                {
                    PlayerHealthManager.Singleton.TakeDamage("A Major Spill", 50);
                }
                else if(ImpactVelocity >= MinorInjuryFallVelocity)
                {
                    PlayerHealthManager.Singleton.TakeDamage("A Minor Spill", 10);
                }

                //Determine if player had crouch jumped
                if (CrouchJumped)
                {
                    CrouchJumped = false;
                    WantsToUncrouch = crouchPressed;
                }

                WasInAir = false;
            }
        }
        else if (Mathf.Abs(Velocity.y) > 4f)
        {
			//GD.Print(Velocity.y);
            WasInAir = true;
            PlayerArms.Singleton.Fall = true;
            ImpactVelocity = -Velocity.y;
        }
        
        //Get input and set direction
        bool userMoving = (Vinput != 0 || Hinput != 0);
        Direction += aim.z * (slipping ? -1f : 1f) * Vinput;
        Direction += aim.x * (slipping ? -1f : 1f) * Hinput;
        Direction = Direction.Normalized();
        
        Vector3 velocityNoGravity = new Vector3(Velocity.x, 0, Velocity.z);

        //Get sprint
        if (SprintToggle)
        {
            if (Input.IsActionJustPressed("sprint"))
            {
                Sprint = !Sprint;
            }
        }
        else
        {
             Sprint = (Input.IsActionPressed("sprint") != SprintToWalk);
        }
        
        //Get speed and acceleration dependant variables
        bool accelerate = Direction.Dot(velocityNoGravity) > 0;

        //Set target speed
        Vector3 targetVelocity;
        if (slipping)
        {
            targetVelocity = Direction * Gravity * (1f - Vector3.Up.Dot(normal));
        }
        else
        {
            targetVelocity = Direction * (Sprint ? (IsOnFloor() ? FullSpeedSprint : Mathf.Max(Velocity.Length(), FullSpeedWalk)) : FullSpeedWalk);
        }

        //Apply gravity
        Velocity.y += Gravity * delta;
        

        //Calculate velocity
        float acceleration = IsOnFloor() || (IsOnWall() && noWalkSlipping) ? AccelerationWalk: AccelerationAir;
        float deacceleration =  IsOnFloor() ? DeaccelerationWalk : DeaccelerationAir;
        velocityNoGravity = velocityNoGravity.LinearInterpolate(targetVelocity, (accelerate ? acceleration : deacceleration) * delta);
        Velocity.x = velocityNoGravity.x;
        Velocity.z = velocityNoGravity.z;

        //Set walking animation blend
        float bob1D = PlayerArms.Singleton.Walk;
        if (userMoving && IsOnFloor())
        {
            bob1D = Mathf.Min(1f, bob1D + ((Sprint ? BobIncrementSprint : BobIncrementWalk) * delta));
        }
        else
        {
            bob1D = Mathf.Max(0.01f, bob1D - ((IsOnFloor() ? BobDecrementFloor : BobDecrementAir) * delta));
        }
        if(HeadBobbing){
            PlayerAnimationTree.Singleton.HeadBobBlend = bob1D;
        }
        PlayerArms.Singleton.Walk = bob1D;
        PlayerAnimationTree.Singleton.HeadBobScale = Sprint ? 1.5f : 1f;

        //Set arm swing blend
        SwingTarget = SwingTarget.LinearInterpolate(Vector2.Zero, 0.05f);
        PlayerArms.Singleton.Swing = SwingTarget;

        //Set step timer
        if(bob1D > 0.1f)
        {
            if(PlayerFeet.Singleton.StepTimer.Paused == true)
            {
                //GD.Print("Not Paused");
                PlayerFeet.Singleton.StepTimer.Paused = false;
            }
        }
        else 
        {
            if (PlayerFeet.Singleton.StepTimer.Paused == false)
            {
                //GD.Print("Paused");
                PlayerFeet.Singleton.StepTimer.Paused = true;
            }
        }
        PlayerFeet.Singleton.StepTimer.WaitTime = Sprint ? 0.25f : 0.5f;

        //Get jump
        PlayerArms.Singleton.Jump = false;
        if ((BunnyHopping ? Input.IsActionPressed("jump") : Input.IsActionJustPressed("jump")) && (playerFeetOverlapsFloor) || Input.IsActionJustPressed("jump") && JumpsLeft > 0)
        {
            PlayerArms.Singleton.Jump = true;
            if(!PlayerFeet.Singleton.JumpPlayer.IsPlaying()) PlayerFeet.Singleton.JumpPlayer.Play();
            Velocity.y = 0f;
            //Velocity += normal * JumpHeight;
            //Velocity.y *= (noWalkSlipping ? 0.1f : 1f);
            if (!slipping && Direction.Dot(velocityNoGravity) < 0f)
            {
                //GD.Print("Reset Velocity");
                Velocity.x = 0f;
                Velocity.z = 0f;
            }
            Velocity += (noWalkSlipping ? normal : Vector3.Up) * JumpHeight; 
            PlayerFeet.Singleton.CanBunnyHop = false;
            _SnapArea.Snap = false;

            if (!playerFeetOverlapsFloor)
            {
                //Update number of jumps and jump label
                JumpsLeft -= 1;
                JumpLabel.Text = "Jumps Left: " + JumpsLeft;
            }
        }

        if (crouchPressed || (Crouched && WantsToUncrouch))
        {
            if (IsOnFloor())
            {
                //Check if uncrouch is possible
                if ((Crouched && StandArea.GetOverlappingBodies().Count == 0) || (!Crouched))
                {
                    //Invert crouch (animation, collision, etc. handled in get set)
                    Crouched = !Crouched;
                    WantsToUncrouch = false;
                }
                else if (crouchPressed)
                {
                    //Invert WantToUncrouch. Player will uncrouch at latest opportunity.
                    WantsToUncrouch = !WantsToUncrouch;
                }
            }
            else if(CanCrouchJump && !Crouched && !CrouchJumped)
            {
                CrouchJumped = true;
                Crouched = true;
                PlayerAnimationTree.Singleton.StateMachineCrouch.Start("CrouchJump");
                Translate(Vector3.Up * 1.1f);
                //GD.Print("Crouch jump.");
            }
        }
        
        //Calculate ledge point
        Vector3 closestLedgePoint = Vector3.Inf;
        bool canClimb = false;
        bool withCrouch = false;
        ClimbLabel.Visible = false;
        if(!Crouched)
        {
            foreach(RayCast LedgeRay in LedgeRays)
            {
                if (LedgeRay.IsColliding())
                {
                    if (LedgeRay.GetCollisionPoint().DistanceTo(Translation) < closestLedgePoint.DistanceTo(Translation))
                    {
                        closestLedgePoint = LedgeRay.GetCollisionPoint();
                        canClimb = true;
                    }
                }
            }
            closestLedgePoint = closestLedgePoint + (Vector3.Up * (StandHeight + 0.075f));

            if (canClimb)
            {
                //Disable all other colliders
                bool[] shapeDisabledState = new bool[OtherCollision.Count];
                for (int i = 0; i < OtherCollision.Count; ++i)
                {
                    //GD.Print("Disabling collision ", OtherCollision[i].Name);
                    shapeDisabledState[i] = OtherCollision[i].Disabled;
                    OtherCollision[i].Disabled = true;
                }

                //Test to see if player has space to climb
                canClimb = !TestMove(new Transform(Quat.Identity, closestLedgePoint), Vector3.Zero);
                if (!canClimb)
                {
                    _CollisionStand.Disabled = true;
                    _CollisionCrouch.Disabled = false;
                    canClimb = !TestMove(new Transform(Quat.Identity, closestLedgePoint), Vector3.Zero);
                    withCrouch = canClimb;
                    _CollisionStand.Disabled = false;
                    _CollisionCrouch.Disabled = true;
                }

                //Reset colliders
                for (int i = 0; i < OtherCollision.Count; ++i)
                {
                    //GD.Print("Reseting other collision", OtherCollision[i], " ", shapeDisabledState[i]);
                    OtherCollision[i].Disabled = shapeDisabledState[i];
                }
            }
            
            ClimbLabel.Visible = canClimb;
            if (canClimb && (bool)_ClimbTimer.Get("wants_to_climb"))
            {
                if (withCrouch)
                {
                    Crouched = true;
                }

                //Set climb point and enter climb state
                ClimbPoint = closestLedgePoint;
                ClimbStep = (ClimbPoint - Translation).Normalized() * ClimbSpeed;
                ClimbDistance = (ClimbPoint - Translation).Length();
                PlayerAnimationTree.Singleton.Climb();
                PlayerAnimationTree.Singleton.HeadBobScale = 0f;
                Climbing = true;

                //Reset ImpactVelocity and zero y Velocity to avoid confusing animations
                ImpactVelocity = Velocity.y = 0f;
            }
        }
        
        //Move Player
        if (userMoving || slipping)
        {
            Velocity = MoveAndSlideWithSnap(Velocity, _SnapArea.Snap ? new Vector3(0, -1f, 0) : Vector3.Zero, Vector3.Up, true, 4, MaxSlopeNoWalk);
        }
        else
        {
            Velocity = MoveAndSlide(Velocity, Vector3.Up, true, 4, MaxSlopeNoWalk);
        }
    }

    private void Climb(float delta){
        Translation = Translation + ClimbStep;
        ClimbDistance -= ClimbSpeed;
        if(ClimbDistance <= 0)
        {
            Translation = ClimbPoint;
            Climbing = false;
            _ClimbTimer.Set("wants_to_climb",false);
            PlayerAnimationTree.Singleton.HeadBobScale = 1f;
        }
    }

    public Dictionary Save()
    {
        Dictionary data = new Dictionary
        {
            { "position" , new Array{Translation.x, Translation.y, Translation.z} },
            { "look" , new Array{CameraAngle.x, CameraAngle.y} },
            { "crouched" , Crouched },
            { "jumps" , JumpCount },
            { "health" , PlayerHealthManager.Singleton.Health }
        };
        return data;
    }

    public void Load(Dictionary data)
    {
        Array a;
        a = (Array)data["position"];
        Translation = new Vector3((float)a[0], (float)a[1], (float)a[2]);
        a = (Array)data["look"];
        CameraAngle = new Vector2((float)a[0], (float)a[1]);
        ApplyCameraAngle();
        Crouched = (bool)data["crouched"];
        JumpCount = (int)data["jumps"];
        PlayerHealthManager.Singleton.SetHealth((int)data["health"]);

        Imobile = false;
        PlayerArms.Singleton.Reset();
    }
}
