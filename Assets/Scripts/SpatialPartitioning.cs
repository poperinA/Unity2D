using System.Collections.Generic;
using UnityEngine;

public class SpatialPartitioning : MonoBehaviour
{
    private int[] cellIndices;
    private int[] startIndices;

    // Add this field to store particle positions
    private List<Vector3> positions;

    // Adjust these parameters based on your simulation
    public float cellSize = 1.0f;
    public float smoothingRadius = 5.0f;

    // Initialize the spatial partitioning data structures
    public void Initialize(List<Vector3> initialPositions)
    {
        int numParticles = initialPositions.Count;
        cellIndices = new int[numParticles];
        startIndices = new int[numParticles];
        positions = initialPositions; // Store particle positions

        // Initialize cellIndices and startIndices based on positions
        for (int i = 0; i < numParticles; i++)
        {
            cellIndices[i] = CalculateCellKey(positions[i]);
        }

        // Sort cellIndices and update startIndices accordingly
        SortCellIndices();
    }

    // Update the spatial partitioning when particles move
    public void UpdateSpatialPartitioning(List<Vector3> updatedPositions)
    {
        // Update particle positions
        positions = updatedPositions;

        // Update cellIndices based on new positions
        for (int i = 0; i < updatedPositions.Count; i++)
        {
            cellIndices[i] = CalculateCellKey(updatedPositions[i]);
        }

        // Sort cellIndices and update startIndices accordingly
        SortCellIndices();
    }

    // Find neighbors of a particle within the smoothing radius
    public List<int> FindNeighbors(Vector3 position)
    {
        List<int> neighbors = new List<int>();

        // Check if positions is not null before accessing it
        if (positions != null)
        {
            // Calculate the cell key for the given position
            int cellKey = CalculateCellKey(position);

            // Use cellKey to find start index in startIndices array
            int startIndex = (cellKey < startIndices.Length) ? startIndices[cellKey] : 0;

            // Loop over particles in the same cell and neighboring cells
            for (int i = startIndex; i < cellIndices.Length; i++)
            {
                if (cellIndices[i] != cellKey)
                    break; // Reached the end of particles in the cell

                // Check if the particle is within the smoothing radius
                if (Vector3.Distance(position, positions[i]) <= smoothingRadius)
                {
                    neighbors.Add(i);
                }
            }
        }

        return neighbors;
    }

    // Calculate the cell key for a given position
    private int CalculateCellKey(Vector3 position)
    {
        int x = Mathf.FloorToInt(position.x / cellSize);
        int y = Mathf.FloorToInt(position.y / cellSize);
        int z = Mathf.FloorToInt(position.z / cellSize);

        // Use prime numbers for hashing to reduce collisions
        int hash = 73856093;
        hash ^= (x * 19349663) ^ (y * 83492791) ^ (z * 16817837);

        // Make sure the hash is non-negative
        return Mathf.Abs(hash);
    }

    // Sort cellIndices and update startIndices accordingly
    private void SortCellIndices()
    {
        List<int> indicesList = new List<int>(cellIndices);
        indicesList.Sort();

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
