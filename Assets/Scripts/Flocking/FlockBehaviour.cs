using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Threading.Tasks;

public class FlockBehaviour : MonoBehaviour
{
    //list of obstacles in the scene
    List<Obstacle> mObstacles = new List<Obstacle>();

    [SerializeField] GameObject[] Obstacles; //array of obstacle game objects

    [SerializeField] BoxCollider2D Bounds; //boundary for the flocking simulation

    //duration of ticks for different flocking behaviors
    public float TickDuration = 1.0f;
    public float TickDurationSeparationEnemy = 0.1f;
    public float TickDurationRandom = 1.0f;

    public int BoidIncr = 100; //increment for adding boids
    public bool useFlocking = false; //toggle for flocking behavior
    public int BatchSize = 100; //batch size for parallel execution

    public List<Flock> flocks = new List<Flock>(); //list of flock configurations

    [SerializeField] public SpatialPartitioning spatialPartitioning; //reference to spatial partitioning script

    void Reset()
    {
        flocks = new List<Flock>()
        {
            new Flock()
        };
    }

    void Start()
    {
        //randomize obstacles placement.
        for (int i = 0; i < Obstacles.Length; ++i)
        {
            //randomly position obstacles within the bounds
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);
            Obstacles[i].transform.position = new Vector3(x, y, 0.0f);

            //add Obstacle and Autonomous components to obstacle game objects
            Obstacle obs = Obstacles[i].AddComponent<Obstacle>();
            Autonomous autono = Obstacles[i].AddComponent<Autonomous>();
            autono.MaxSpeed = 1.0f;
            obs.mCollider = Obstacles[i].GetComponent<CircleCollider2D>();
            mObstacles.Add(obs);
        }

        //create flocks and initialize flocking behaviors
        foreach (Flock flock in flocks)
        {
            CreateFlock(flock);
        }

        //start coroutines for different flocking behaviors
        StartCoroutine(Coroutine_Flocking());
        StartCoroutine(Coroutine_Random());
        StartCoroutine(Coroutine_AvoidObstacles());
        StartCoroutine(Coroutine_SeparationWithEnemies());
        StartCoroutine(Coroutine_Random_Motion_Obstacles());
    }

    //create initial flock with random positions
    void CreateFlock(Flock flock)
    {
        for (int i = 0; i < flock.numBoids; ++i)
        {
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

            AddBoid(x, y, flock);
        }
    }

    //update method for handling inputs and flocking rules
    void Update()
    {
        HandleInputs();
        Rule_CrossBorder();
        Rule_CrossBorder_Obstacles();
    }

    //handle keyboard inputs
    void HandleInputs()
    {
        if (EventSystem.current.IsPointerOverGameObject() || enabled == false)
        {
            return;
        }

        //add boids when space key is pressed
        if (Input.GetKeyDown(KeyCode.Space))
        {
            AddBoids(BoidIncr);
        }
    }

    //add a specified number of boids with random positions to the flock
    void AddBoids(int count)
    {
        for (int i = 0; i < count; ++i)
        {
            float x = Random.Range(Bounds.bounds.min.x, Bounds.bounds.max.x);
            float y = Random.Range(Bounds.bounds.min.y, Bounds.bounds.max.y);

            AddBoid(x, y, flocks[0]);
        }
        flocks[0].numBoids += count;
    }

    //add a single boid with a specified position to the flock
    void AddBoid(float x, float y, Flock flock)
    {
        GameObject obj = Instantiate(flock.PrefabBoid);
        obj.name = "Boid_" + flock.name + "_" + flock.mAutonomous.Count;
        obj.transform.position = new Vector3(x, y, 0.0f);
        Autonomous boid = obj.GetComponent<Autonomous>();
        flock.mAutonomous.Add(boid);
        boid.MaxSpeed = flock.maxSpeed;
        boid.RotationSpeed = flock.maxRotationSpeed;
    }

    //calculate the Euclidean distance between two autonomous agents
    static float Distance(Autonomous a1, Autonomous a2)
    {
        return (a1.transform.position - a2.transform.position).magnitude;
    }

    //execute flocking behaviors for a single boid
    void Execute(Flock flock, int i)
    {
        Vector3 flockDir = Vector3.zero;
        Vector3 separationDir = Vector3.zero;
        Vector3 cohesionDir = Vector3.zero;

        float speed = 0.0f;
        float separationSpeed = 0.0f;

        int count = 0;
        int separationCount = 0;
        Vector3 steerPos = Vector3.zero;

        Autonomous curr = flock.mAutonomous[i];
        for (int j = 0; j < flock.numBoids; ++j)
        {
            Autonomous other = flock.mAutonomous[j];
            float dist = (curr.transform.position - other.transform.position).magnitude;
            if (i != j && dist < flock.visibility)
            {
                speed += other.Speed;
                flockDir += other.TargetDirection;
                steerPos += other.transform.position;
                count++;
            }
            if (i != j)
            {
                if (dist < flock.separationDistance)
                {
                    Vector3 targetDirection = (
                        curr.transform.position -
                        other.transform.position).normalized;

                    separationDir += targetDirection;
                    separationSpeed += dist * flock.weightSeparation;
                }
            }
        }
        if (count > 0)
        {
            speed = speed / count;
            flockDir = flockDir / count;
            flockDir.Normalize();

            steerPos = steerPos / count;
        }

        if (separationCount > 0)
        {
            separationSpeed = separationSpeed / count;
            separationDir = separationDir / separationSpeed;
            separationDir.Normalize();
        }

        curr.TargetDirection =
            flockDir * speed * (flock.useAlignmentRule ? flock.weightAlignment : 0.0f) +
            separationDir * separationSpeed * (flock.useSeparationRule ? flock.weightSeparation : 0.0f) +
            (steerPos - curr.transform.position) * (flock.useCohesionRule ? flock.weightCohesion : 0.0f);
    }

    //coroutine for executing flocking behaviors
    IEnumerator Coroutine_Flocking()
    {
        int tasksPerFrame = 10;

        while (true)
        {
            if (useFlocking)
            {
                foreach (var flock in flocks)
                {
                    List<Autonomous> autonomousList = flock.mAutonomous;

                    //parallelize execution of flocking behaviors
                    for (int i = 0; i < autonomousList.Count; i += tasksPerFrame)
                    {
                        int endIndex = Mathf.Min(i + tasksPerFrame, autonomousList.Count);
                        MainThreadDispatcher.Instance.Enqueue(() =>
                        {
                            for (int j = i; j < endIndex; j++)
                            {
                                Execute(flock, j);
                            }
                        });
                    }
                }
            }

            yield return new WaitForSeconds(TickDuration);
        }
    }

    void SeparationWithEnemies_Internal(
        List<Autonomous> boids,
        List<Autonomous> enemies,
        float sepDist,
        float sepWeight)
    {
        for (int i = 0; i < boids.Count; ++i)
        {
            for (int j = 0; j < enemies.Count; ++j)
            {
                float dist = (
                    enemies[j].transform.position -
                    boids[i].transform.position).magnitude;
                if (dist < sepDist)
                {
                    Vector3 targetDirection = (
                        boids[i].transform.position -
                        enemies[j].transform.position).normalized;

                    boids[i].TargetDirection += targetDirection;
                    boids[i].TargetDirection.Normalize();

                    boids[i].TargetSpeed += dist * sepWeight;
                    boids[i].TargetSpeed /= 2.0f;
                }
            }
        }
    }

    IEnumerator Coroutine_SeparationWithEnemies()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (!flock.useFleeOnSightEnemyRule || flock.isPredator) continue;

                foreach (Flock enemies in flocks)
                {
                    if (!enemies.isPredator) continue;

                    SeparationWithEnemies_Internal(
                        flock.mAutonomous,
                        enemies.mAutonomous,
                        flock.enemySeparationDistance,
                        flock.weightFleeOnSightEnemy);
                }
            }
            yield return null;
        }
    }

    IEnumerator Coroutine_AvoidObstacles()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (flock.useAvoidObstaclesRule)
                {
                    List<Autonomous> autonomousList = flock.mAutonomous;

                    //iterate through each autonomous agent to find neighbors and avoid obstacles
                    for (int i = 0; i < autonomousList.Count; ++i)
                    {
                        //use spatial partitioning to find neighbors for the current autonomous agent
                        Vector3 currentPosition = autonomousList[i].transform.position;
                        List<int> neighbors = spatialPartitioning.FindNeighbors(currentPosition);

                        //loop through the neighbors and avoid obstacles
                        foreach (int neighborIndex in neighbors)
                        {
                            AvoidObstacle(autonomousList, i, mObstacles, neighborIndex, flock);
                        }
                    }
                }
            }
            yield return null;
        }
    }


    void AvoidObstacle(List<Autonomous> autonomousList, int currentIndex, List<Obstacle> obstacles, int obstacleIndex, Flock flock)
    {
        float dist = (obstacles[obstacleIndex].transform.position - autonomousList[currentIndex].transform.position).magnitude;
        if (dist < obstacles[obstacleIndex].AvoidanceRadius)
        {
            Vector3 targetDirection = (autonomousList[currentIndex].transform.position - obstacles[obstacleIndex].transform.position).normalized;

            autonomousList[currentIndex].TargetDirection += targetDirection * flock.weightAvoidObstacles;
            autonomousList[currentIndex].TargetDirection.Normalize();
        }
    }

    IEnumerator Coroutine_Random_Motion_Obstacles()
    {
        while (true)
        {
            for (int i = 0; i < Obstacles.Length; ++i)
            {
                Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
                float rand = Random.Range(0.0f, 1.0f);
                autono.TargetDirection.Normalize();
                float angle = Mathf.Atan2(autono.TargetDirection.y, autono.TargetDirection.x);

                if (rand > 0.5f)
                {
                    angle += Mathf.Deg2Rad * 45.0f;
                }
                else
                {
                    angle -= Mathf.Deg2Rad * 45.0f;
                }
                Vector3 dir = Vector3.zero;
                dir.x = Mathf.Cos(angle);
                dir.y = Mathf.Sin(angle);

                autono.TargetDirection += dir * 0.1f;
                autono.TargetDirection.Normalize();

                float speed = Random.Range(1.0f, autono.MaxSpeed);
                autono.TargetSpeed += speed;
                autono.TargetSpeed /= 2.0f;
            }
            yield return new WaitForSeconds(2.0f);
        }
    }

    IEnumerator Coroutine_Random()
    {
        while (true)
        {
            foreach (Flock flock in flocks)
            {
                if (flock.useRandomRule)
                {
                    List<Autonomous> autonomousList = flock.mAutonomous;
                    for (int i = 0; i < autonomousList.Count; ++i)
                    {
                        float rand = Random.Range(0.0f, 1.0f);
                        autonomousList[i].TargetDirection.Normalize();
                        float angle = Mathf.Atan2(autonomousList[i].TargetDirection.y, autonomousList[i].TargetDirection.x);

                        if (rand > 0.5f)
                        {
                            angle += Mathf.Deg2Rad * 45.0f;
                        }
                        else
                        {
                            angle -= Mathf.Deg2Rad * 45.0f;
                        }
                        Vector3 dir = Vector3.zero;
                        dir.x = Mathf.Cos(angle);
                        dir.y = Mathf.Sin(angle);

                        autonomousList[i].TargetDirection += dir * flock.weightRandom;
                        autonomousList[i].TargetDirection.Normalize();

                        float speed = Random.Range(1.0f, autonomousList[i].MaxSpeed);
                        autonomousList[i].TargetSpeed += speed * flock.weightSeparation;
                        autonomousList[i].TargetSpeed /= 2.0f;
                    }
                }
            }
            yield return new WaitForSeconds(TickDurationRandom);
        }
    }

    void Rule_CrossBorder_Obstacles()
    {
        for (int i = 0; i < Obstacles.Length; ++i)
        {
            Autonomous autono = Obstacles[i].GetComponent<Autonomous>();
            Vector3 pos = autono.transform.position;
            if (autono.transform.position.x > Bounds.bounds.max.x)
            {
                pos.x = Bounds.bounds.min.x;
            }
            if (autono.transform.position.x < Bounds.bounds.min.x)
            {
                pos.x = Bounds.bounds.max.x;
            }
            if (autono.transform.position.y > Bounds.bounds.max.y)
            {
                pos.y = Bounds.bounds.min.y;
            }
            if (autono.transform.position.y < Bounds.bounds.min.y)
            {
                pos.y = Bounds.bounds.max.y;
            }
            autono.transform.position = pos;
        }
    }

    void Rule_CrossBorder()
    {
        foreach (Flock flock in flocks)
        {
            List<Autonomous> autonomousList = flock.mAutonomous;
            if (flock.bounceWall)
            {
                for (int i = 0; i < autonomousList.Count; ++i)
                {
                    Vector3 pos = autonomousList[i].transform.position;
                    if (autonomousList[i].transform.position.x + 5.0f > Bounds.bounds.max.x)
                    {
                        autonomousList[i].TargetDirection.x = -1.0f;
                    }
                    if (autonomousList[i].transform.position.x - 5.0f < Bounds.bounds.min.x)
                    {
                        autonomousList[i].TargetDirection.x = 1.0f;
                    }
                    if (autonomousList[i].transform.position.y + 5.0f > Bounds.bounds.max.y)
                    {
                        autonomousList[i].TargetDirection.y = -1.0f;
                    }
                    if (autonomousList[i].transform.position.y - 5.0f < Bounds.bounds.min.y)
                    {
                        autonomousList[i].TargetDirection.y = 1.0f;
                    }
                    autonomousList[i].TargetDirection.Normalize();
                }
            }
            else
            {
                for (int i = 0; i < autonomousList.Count; ++i)
                {
                    Vector3 pos = autonomousList[i].transform.position;
                    if (autonomousList[i].transform.position.x > Bounds.bounds.max.x)
                {
                    pos.x = Bounds.bounds.min.x;
                }
                if (autonomousList[i].transform.position.x < Bounds.bounds.min.x)
                {
                    pos.x = Bounds.bounds.max.x;
                }
                if (autonomousList[i].transform.position.y > Bounds.bounds.max.y)
                {
                  pos.y = Bounds.bounds.min.y;
                }
                 if (autonomousList[i].transform.position.y < Bounds.bounds.min.y)
                {
                 pos.y = Bounds.bounds.max.y;
                 }
                autonomousList[i].transform.position = pos;
                }
            }
        }
    }
}
