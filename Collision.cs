using System;
using UnityEngine;

public class Collision : MonoBehaviour
{
    [Header("Raycast Settings")]
    public float groundRayLength = 1.0f;
    public float wallRayLength = 0.6f;
    public float topRayLength = 0.25f;
    public float ledgeCheckSpacing = 0.1f;

    [Header("Layers")]
    public LayerMask groundLayer;
    public LayerMask wallLayer;

    [Header("Surface Layers")]
    public LayerMask dirtLayer;
    public LayerMask stoneLayer;
    public LayerMask waterLayer;
    public LayerMask metalLayer;
    public LayerMask woodLayer;

    [Header("General Detection")]
    public LayerMask groundDetectionLayers;
    public LayerMask wallDetectionLayers;

    [Header("Corner Push Settings")]
    [Range(0f, 1f)]
    [Tooltip("Ayak seviyesine denk gelen Y oranı (0-1)")]
    public float cornerFootFraction = 0.25f;
    [Range(0f, 1f)]
    [Tooltip("Kafa seviyesine denk gelen Y oranı (0-1)")]
    public float cornerHeadFraction = 0.85f;
    [Tooltip("Collider yarı-genişliği + bu kadar ileri mesafe")]
    public float cornerPushDist = 0.02f;
    [Tooltip("OverlapCircle yarıçapı")]
    public float cornerPushRadius = 0.05f;
    [HideInInspector]
    public bool isAtCornerPush;
    [HideInInspector]
    public bool isAtCornerLedge;

    [Header("State")]
    public bool onGround;
    public bool onWall;
    public bool onRightWall;
    public bool onLeftWall;
    public int wallSide;
    public bool canClimbToTop;

    public SurfaceType currentGroundSurfaceType { get; private set; } = SurfaceType.Other;
    public SurfaceType currentWallSurfaceType { get; private set; } = SurfaceType.Other;

    private void FixedUpdate()
    {
        SurfaceType wallSurface = SurfaceType.Other;
        Debug.Log("Wall Surface Detected: " + wallSurface);

        Vector2 pos = transform.position;

        // Ground detection
        onGround = false;
        Vector2[] groundOrigins = { pos + Vector2.left * 0.3f, pos, pos + Vector2.right * 0.3f };
        foreach (var origin in groundOrigins)
        {
            var hit = Physics2D.Raycast(origin + Vector2.down * 0.1f,
                                        Vector2.down,
                                        groundRayLength,
                                        groundDetectionLayers);
            Debug.DrawRay(origin + Vector2.down * 0.1f,
                          Vector2.down * groundRayLength,
                          Color.green);
            if (hit.collider != null)
            {
                onGround = true;
                currentGroundSurfaceType = DetectSurfaceTypeFromLayer(hit);
                break;
            }
        }

        // Wall detection
        onLeftWall = CheckWall(Vector2.left);
        onRightWall = CheckWall(Vector2.right);
        onWall = onLeftWall || onRightWall;
        wallSide = onRightWall ? -1 : (onLeftWall ? 1 : 0);

        if (onWall)
        {
            Vector2 dir = onRightWall ? Vector2.right : Vector2.left;
            CheckWallSurface(dir);
            canClimbToTop = CheckTopOfWall(dir);
            // Corner push when head-ray misses (independent of climbable)
            isAtCornerPush = CheckCornerPush(dir);
            // Now flag ledge-slide only when truly climbable top corner
            isAtCornerLedge = canClimbToTop && isAtCornerPush;
        }
        else
        {
            currentWallSurfaceType = SurfaceType.Other;
            canClimbToTop = false;
            isAtCornerPush = false;
        }
    }

    private bool CheckWall(Vector2 dir)
    {
        Vector2 pos = transform.position;
        Vector2[] offsets = {
            Vector2.up * 0.5f,
            Vector2.up * 0.35f,
            Vector2.up * 0.2f,
            Vector2.zero,
            Vector2.down * 0.2f,
            Vector2.down * 0.35f,
            Vector2.down * 0.5f
        };
        foreach (var off in offsets)
        {
            var hit = Physics2D.Raycast(pos + off,
                                        dir,
                                        wallRayLength,
                                        wallDetectionLayers);
            Debug.DrawRay(pos + off,
                          dir * wallRayLength,
                          Color.red);
            if (hit.collider != null) return true;
        }
        return false;
    }

    private void CheckWallSurface(Vector2 dir)
    {
        Vector2 pos = transform.position;
        Vector2[] offsets = {
            Vector2.up * 0.5f,
            Vector2.up * 0.35f,
            Vector2.up * 0.2f,
            Vector2.zero,
            Vector2.down * 0.2f,
            Vector2.down * 0.35f,
            Vector2.down * 0.5f
        };
        foreach (var off in offsets)
        {
            var hit = Physics2D.Raycast(pos + off,
                                        dir,
                                        wallRayLength,
                                        wallDetectionLayers);
            Debug.DrawRay(pos + off,
                          dir * wallRayLength,
                          Color.cyan);
            if (hit.collider != null)
            {
                currentWallSurfaceType = DetectSurfaceTypeFromLayer(hit);
                return;
            }
        }
        currentWallSurfaceType = SurfaceType.Other;
    }

    private bool CheckTopOfWall(Vector2 dir)
    {
        Vector2 pos = transform.position;
        float startOffsetY = 0.2f;
        float horizontalOffset = 0.4f;
        float verticalRayLength = 0.8f;
        float forwardRayLength = 0.4f;

        int hits = 0;
        for (int i = -2; i <= 2; i++)
        {
            Vector2 origin = pos + Vector2.up * startOffsetY + dir * (horizontalOffset + i * 0.02f);
            var downHit = Physics2D.Raycast(origin,
                                            Vector2.down,
                                            verticalRayLength,
                                            groundDetectionLayers);
            var forwardHit = Physics2D.Raycast(origin,
                                               dir,
                                               forwardRayLength,
                                               wallDetectionLayers);
            Debug.DrawRay(origin, Vector2.down * verticalRayLength, Color.yellow);
            Debug.DrawRay(origin, dir * forwardRayLength, Color.blue);
            if (downHit.collider != null && forwardHit.collider == null)
                hits++;
        }
        return hits >= 2;
    }

    private bool CheckCornerPush(Vector2 dir)
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null) return false;
        var b = box.bounds;

        // Foot-level origin
        float footY = b.min.y + b.size.y * cornerFootFraction;
        Vector2 fOrigin = new Vector2(b.center.x, footY)
                          + dir * (b.extents.x + cornerPushDist);

        // Head-level origin
        float headY = b.min.y + b.size.y * cornerHeadFraction;
        Vector2 hOrigin = new Vector2(b.center.x, headY)
                          + dir * (b.extents.x + cornerPushDist);

        // Cast both rays
        bool footHit = Physics2D.Raycast(fOrigin, dir, wallRayLength, wallDetectionLayers);
        bool headHit = Physics2D.Raycast(hOrigin, dir, wallRayLength, wallDetectionLayers);

        Debug.DrawRay(fOrigin, dir * wallRayLength, Color.magenta);
        Debug.DrawRay(hOrigin, dir * wallRayLength, Color.white);

        // Push only when BOTH rays are still hitting: character is hooked on the lip.
        return footHit && headHit;
    }

    private SurfaceType DetectSurfaceTypeFromLayer(RaycastHit2D hit)
    {
        int layer = hit.collider.gameObject.layer;
        if (((1 << layer) & dirtLayer) != 0) return SurfaceType.Dirt;
        if (((1 << layer) & stoneLayer) != 0) return SurfaceType.Stone;
        if (((1 << layer) & waterLayer) != 0) return SurfaceType.Water;
        if (((1 << layer) & metalLayer) != 0) return SurfaceType.Metal;
        if (((1 << layer) & woodLayer) != 0) return SurfaceType.Wood;
        return SurfaceType.Other;
    }
    public SurfaceType GetWallSurfaceType()
    {
        Vector2 origin = (Vector2)transform.position + Vector2.right * wallSide * 0.35f + Vector2.up * 0.2f;
        Vector2 direction = wallSide == 1 ? Vector2.right : Vector2.left;

        float rayDistance = 0.1f;
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, rayDistance);

        Debug.DrawRay(origin, direction * rayDistance, Color.magenta);

        if (hit.collider != null)
        {
            int layer = hit.collider.gameObject.layer;

            if (((1 << layer) & LayerMask.GetMask("Stone")) != 0) return SurfaceType.Stone;
            if (((1 << layer) & LayerMask.GetMask("Dirt")) != 0) return SurfaceType.Dirt;
            if (((1 << layer) & LayerMask.GetMask("Metal")) != 0) return SurfaceType.Metal;
            if (((1 << layer) & LayerMask.GetMask("Wood")) != 0) return SurfaceType.Wood;
            if (((1 << layer) & LayerMask.GetMask("Water")) != 0) return SurfaceType.Water;
        }

        return SurfaceType.Other;
    }



    public enum SurfaceType { Other, Dirt, Stone, Water, Metal, Wood }
}