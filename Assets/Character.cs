using UnityEngine;

public class Character : MonoBehaviour {
  public enum State { Grounded = 0, Airborne, WallSlide, WallJump }

  public static Vector3 CameraPosition(Vector3 currentPosition, float targetHeight, float epsilon, float dt) {
    var desiredPosition = currentPosition;

    desiredPosition.y = targetHeight;
    return Vector3.Lerp(currentPosition, desiredPosition, 1 - Mathf.Pow(epsilon, dt));
  }

  public static Quaternion CameraRotation(Quaternion currentRotation, Vector3 currentPosition, float targetHeight, float epsilon, float dt) {
    var targetPosition = new Vector3(0, targetHeight, 0);
    var towardsTarget = targetPosition - currentPosition;
    var desiredRotation = Quaternion.LookRotation(towardsTarget, Vector3.up);

    return Quaternion.Slerp(currentRotation, desiredRotation, 1 - Mathf.Pow(epsilon, dt));
  }

  [Header("References")]
  public CharacterController Controller;
  public ParticleSystem SlideParticles;
  public AudioSource SlideAudioSource;
  public AudioClip GruntAudioClip;
  public AudioClip HitAudioClip;
  public Animator Animator;
  public Camera Camera;

  [Header("State")]
  public Vector3 velocity;
  public State currentState;
  public float remainingWallJumpTime;

  [Header("Configuration")]
  public float riseGravityMultiplier;
  public float fallGravityMultiplier;
  public float groundSpeed;
  public float airSpeed;
  public float verticalGroundedLaunchImpulse;
  public float horizontalLaunchImpulse;
  public float verticalLaunchImpulse;
  public float horizontalBackflipImpulse;
  public float verticalBackflipImpulse;
  public float wallDistanceBuffer;
  public float wallFriction;
  public float genericFriction;
  public float maxWallProximity;
  public float maxGroundProximity;
  public float wallJumpDuration;
  public float minimumCameraHeight;
  public float cameraPositionEpsilon;
  public float cameraRotationEpsilon;
  public float maxYVelocityForSlidingEffects = -10;


  void Update() {
    var horizontal = Input.GetAxis("Horizontal");
    var jumpDown = Input.GetButtonDown("Jump");
    var backflipDown = Input.GetButtonDown("Fire1");
    var walldistance = Controller.radius + Controller.skinWidth + wallDistanceBuffer;
    var didHitWall = Physics.Raycast(transform.position, transform.forward, out RaycastHit wallContactHit, walldistance);
    var didHitWallProximity = Physics.Raycast(transform.position, transform.forward, out RaycastHit wallProximityHit, walldistance + maxWallProximity);
    var didHitGroundProximity = Physics.Raycast(transform.position, -transform.up, out RaycastHit groundProximityHit, walldistance + maxGroundProximity);
    var wallProximity = didHitWallProximity ? (1 - (wallProximityHit.distance - Controller.skinWidth - Controller.radius) / (walldistance + maxWallProximity)) : 0;
    var groundProximity = didHitGroundProximity ? (1 - (groundProximityHit.distance - Controller.skinWidth) / (walldistance + maxGroundProximity)) : 0;

    switch (currentState) {
    case State.Grounded:
      if (jumpDown) {
        AudioSource.PlayClipAtPoint(GruntAudioClip, transform.position);
        velocity = transform.forward * horizontalLaunchImpulse + transform.up * verticalGroundedLaunchImpulse;
        currentState = State.Airborne;
      } else {
        velocity += Physics.gravity * fallGravityMultiplier * Time.deltaTime;
      }
    break;

    case State.Airborne:
      if (Controller.isGrounded) {
        velocity = Physics.gravity * fallGravityMultiplier * Time.deltaTime;
        currentState = State.Grounded;
      } else if (didHitWall) {
        SlideParticles.Play();
        SlideAudioSource.volume = 0;
        SlideAudioSource.Play();
        AudioSource.PlayClipAtPoint(HitAudioClip, transform.position);
        velocity -= Vector3.Project(velocity, wallContactHit.normal);
        currentState = State.WallSlide;
      }
      velocity += Physics.gravity * fallGravityMultiplier * Time.deltaTime;
    break;

    case State.WallSlide:
      if (!didHitWall) {
        if (!Controller.isGrounded) {
          velocity += Physics.gravity * fallGravityMultiplier * Time.deltaTime;
          SlideAudioSource.Stop();
          SlideParticles.Stop();
          currentState = State.Airborne;
        } else {
          velocity = Physics.gravity * fallGravityMultiplier * Time.deltaTime;
          SlideAudioSource.Stop();
          SlideParticles.Stop();
          currentState = State.Grounded;
        }
      } else {
        if (backflipDown) {
          velocity = new Vector3(wallContactHit.normal.x * horizontalBackflipImpulse, verticalBackflipImpulse, 0f);
          AudioSource.PlayClipAtPoint(GruntAudioClip, transform.position);
          SlideAudioSource.Stop();
          SlideParticles.Stop();
          currentState = State.Airborne;
          Animator.SetTrigger("Backflip");
        } else if (jumpDown) {
          velocity = Vector3.zero;
          SlideAudioSource.Stop();
          SlideParticles.Stop();
          remainingWallJumpTime = wallJumpDuration;
          currentState = State.WallJump;
        } else if (Controller.isGrounded) {
          velocity += Physics.gravity * fallGravityMultiplier * Time.deltaTime;
          SlideAudioSource.Stop();
          SlideParticles.Stop();
          currentState = State.Grounded;
        } else {
          SlideAudioSource.volume = Mathf.InverseLerp(0, maxYVelocityForSlidingEffects, -velocity.y);
          velocity += Physics.gravity * fallGravityMultiplier * Time.deltaTime;
        }
      }
    break;

    case State.WallJump:
      if (!didHitWall) {
        if (!Controller.isGrounded) {
          velocity += Physics.gravity * fallGravityMultiplier * Time.deltaTime;
          currentState = State.Airborne;
        } else {
          velocity = Physics.gravity * fallGravityMultiplier * Time.deltaTime;
          currentState = State.Grounded;
        }
      } else {
        remainingWallJumpTime = Mathf.Max(remainingWallJumpTime - Time.deltaTime, 0);
        if (remainingWallJumpTime <= 0) {
          AudioSource.PlayClipAtPoint(GruntAudioClip, transform.position);
          transform.forward = new Vector3(wallContactHit.normal.x, 0, wallContactHit.normal.z).normalized;
          velocity = new Vector3(wallContactHit.normal.x * horizontalLaunchImpulse, verticalLaunchImpulse, 0f);
          currentState = State.Airborne;
        }
      }
    break;
    }

    Controller.Move(velocity * Time.deltaTime);

    var cameraPosition = CameraPosition(Camera.transform.position, Mathf.Max(minimumCameraHeight, transform.position.y), cameraPositionEpsilon, Time.deltaTime);
    var subjectCenteredPosition = new Vector3(0, transform.position.y, 0);
    var cameraRotation = CameraRotation(Camera.transform.rotation, cameraPosition, transform.position.y, cameraRotationEpsilon, Time.deltaTime);

    Camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
    Animator.SetInteger("State", (int)currentState);
    Animator.SetFloat("NearWall", wallProximity);
    Animator.SetFloat("NearGround", groundProximity);
  }
}