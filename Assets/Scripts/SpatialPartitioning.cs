using System.Collections.Generic;
using UnityEngine;

public class SpatialPartitioning : MonoBehaviour
{
    //arrays to store cell indices and start indices for particles
    private int[] cellIndices;
    private int[] startIndices;

    //list to store particle positions
    private List<Vector3> positions;

    //adjustable in inspector
    public float cellSize = 1.0f;
    public float smoothingRadius = 5.0f;

    //initialize the spatial partitioning data structures
    public void Initialize(List<Vector3> initialPositions)
    {
        //get the number of particles
        int numParticles = initialPositions.Count;

        //initialize arrays based on the number of particles
        cellIndices = new int[numParticles];
        startIndices = new int[numParticles];
        positions = initialPositions; //store particle positions

        //initializes cellIndices and startIndices based on the positions
        for (int i = 0; i < numParticles; i++)
        {
            cellIndices[i] = CalculateCellKey(positions[i]);
        }

        //sort cellIndices and update startIndices accordingly
        SortCellIndices();
    }

    //to update the spatial partitioning when particles move
    public void UpdateSpatialPartitioning(List<Vector3> updatedPositions)
    {
        //update particle positions
        positions = updatedPositions;

        //update cellIndices based on new positions
        for (int i = 0; i < updatedPositions.Count; i++)
        {
            cellIndices[i] = CalculateCellKey(updatedPositions[i]);
        }

        //sort cellIndices and update startIndices accordingly
        SortCellIndices();
    }

    //find neighbors of a particle within the smoothing radius
    public List<int> FindNeighbors(Vector3 position)
    {
        List<int> neighbors = new List<int>();

        //check if positions is not null before accessing it
        if (positions != null)
        {
            //calculate the cell key for the given position
            int cellKey = CalculateCellKey(position);

            //use cellKey to find start index in startIndices array
            int startIndex = (cellKey < startIndices.Length) ? startIndices[cellKey] : 0;

            //loop over particles in the same cell and neighboring cells
            for (int i = startIndex; i < cellIndices.Length; i++)
            {
                if (cellIndices[i] != cellKey)
                    break; //reached the end of particles in the cell

                //check if the particle is within the smoothing radius
                if (Vector3.Distance(position, positions[i]) <= smoothingRadius)
                {
                    neighbors.Add(i);
                }
            }
        }

        return neighbors;
    }

    //calculate the cell key for a given position
    private int CalculateCellKey(Vector3 position)
    {
        int x = Mathf.FloorToInt(position.x / cellSize);
        int y = Mathf.FloorToInt(position.y / cellSize);
        int z = Mathf.FloorToInt(position.z / cellSize);

        //use prime numbers for hashing to reduce collisions
        int hash = 73856093;
        hash ^= (x * 19349663) ^ (y * 83492791) ^ (z * 16817837);

        //make sure the hash is non-negative
        return Mathf.Abs(hash);
    }

    //sort cellIndices and update startIndices accordingly
    private void SortCellIndices()
    {
        //create a list from cellIndices for sorting
        List<int> indicesList = new List<int>(cellIndices);
        indicesList.Sort();

        //update cellIndices and startIndices
        for (int i = 0; i < indicesList.Count; i++)
        {
            cellIndices[i] = indicesList[i];

            if (i == 0 || cellIndices[i] != cellIndices[i - 1])
            {
                startIndices[cellIndices[i]] = i;
            }
        }
    }
}
