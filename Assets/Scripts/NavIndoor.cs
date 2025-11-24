/******************************************************************************************
 *  NavIndoor.cs
 *
 *  Summary
 *  -------
 *  A simple indoor AR navigation system.
 *
 *  • It watches for tracked images (via ARTrackedImageManager). When a new image is
 *    detected, it spawns a prefab that contains a NavMesh surface and a set of
 *    NavDestination markers.
 *  • The NavMesh surface is built (or rebuilt) around the spawned prefab so the
 *    Unity NavMesh system can compute paths.
 *  • Every frame the script calculates a path from the player’s current position
 *    to the first NavDestination and draws that path with a LineRenderer.
 *  • The navigation update can run either in Update() or in a coroutine at a fixed 
    *    interval (configurable).
 *
 ******************************************************************************************/

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Main indoor navigation controller
/// </summary>
public class NavIndoor : MonoBehaviour
{
    // ---------- Serialized fields ----------
    [SerializeField] private Transform player;
    [SerializeField] private ARTrackedImageManager m_trackedImageManager;
    [SerializeField] private GameObject trackedImagePrefab;
    [SerializeField] private LineRenderer line;

    // ---------- Runtime state ----------
    private List<NavDestination> destinations = new List<NavDestination>();
    private NavMeshSurface navSurface;
    private NavMeshPath navPath;
    private GameObject navBase;

    // Choose the update strategy:
    [SerializeField] private bool useCoroutine = true;   // expose in inspector for quick testing
    private Coroutine navRoutine;

    #region Unity callbacks
    private void Awake()
    {
        // Prevent the device from sleeping while the AR session runs.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    private void OnEnable()  => m_trackedImageManager.trackablesChanged.AddListener(OnTrackedImagesChanged);
    private void OnDisable() => m_trackedImageManager.trackablesChanged.RemoveListener(OnTrackedImagesChanged);

    private void Start()
    {
        navPath = new NavMeshPath();

        if (useCoroutine)
            navRoutine = StartCoroutine(NavigationLoop());
    }

    private void Update()
    {
        if (!useCoroutine) TryUpdateNavigation();
    }

    private void OnDestroy()
    {
        if (navRoutine != null) StopCoroutine(navRoutine);
    }
    #endregion

    #region Navigation logic
    /// <summary>
    /// Core routine that decides whether a path can be computed and renders it.
    /// Returns early if prerequisites are missing.
    /// </summary>
    private void TryUpdateNavigation()
    {
        if (navBase == null || destinations.Count == 0 || navSurface == null)
            return;

        // Uncomment to rebuild the mesh dynamically.
        // navSurface.BuildNavMesh();

        CalculatePath(player.position, destinations[0].transform.position);
        RenderPath();
    }

    private void CalculatePath(Vector3 from, Vector3 to)
    {
        NavMesh.CalculatePath(from, to, NavMesh.AllAreas, navPath);
    }

    private void RenderPath()
    {
        if (navPath.status == NavMeshPathStatus.PathComplete)
        {
            line.positionCount = navPath.corners.Length;
            line.SetPositions(navPath.corners);
        }
        else
        {
            line.positionCount = 0;
        }
    }

    /// <summary>
    /// Coroutine version that runs at a configurable interval (default 0.1 s).
    /// Adjust the wait time to balance responsiveness vs. CPU load.
    /// </summary>
    private IEnumerator NavigationLoop(float interval = 0.1f)
    {
        var wait = new WaitForSeconds(interval);
        while (true)
        {
            TryUpdateNavigation();
            yield return wait;
        }
    }
    #endregion

    #region AR image handling
    private void OnTrackedImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        foreach (var added in args.added)
        {
            // Spawn a fresh navigation base for the newly detected image.
            navBase = Instantiate(trackedImagePrefab);

            // Cache all NavDestination components under the prefab.
            destinations = navBase.GetComponentsInChildren<NavDestination>().ToList();

            // Find the NavMeshSurface that will generate the walkable area.
            navSurface = navBase.GetComponentInChildren<NavMeshSurface>();
        }

        foreach (var updated in args.updated)
        {
            // Keep the navigation base aligned with the tracked image pose.
            // Only Y‑axis rotation matters for a planar NavMesh.
            navBase.transform.SetPositionAndRotation(
                updated.pose.position,
                Quaternion.Euler(0, updated.pose.rotation.eulerAngles.y, 0));
        }

        // clean up when an image disappears.
        foreach (var removed in args.removed)
        {
            // if (navBase != null) Destroy(navBase);
        }
    }
    #endregion
}