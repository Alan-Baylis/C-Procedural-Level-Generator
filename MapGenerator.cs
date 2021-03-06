﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;



public class MapGenerator : MonoBehaviour {
	public int mapSize;
	[Range( 40,60)]
	public int fillPercent;
	public bool useRandomSeed;
	public string seed;
	public int [,] map;
	public int smoothAmount;
	public int minRoomSize;
	public int minWallSize;
	bool start;
	bool end;
	public bool mapDebug;
	Edge parentEdge;
	public Area self;

	/// <summary>
	/// Main function of the map generator. This calls all other functions.
	/// </summary>
	/// <param name="_self">Self.</param>
	/// <param name="_parentEdge">Parent edge.</param>
	/// <param name="size">Size.</param>
	/// <param name="_start">If set to <c>true</c> start.</param>
	public void CreateMap(Area _self, Edge _parentEdge, int size = 50, bool _start = false, bool _end = false){
		start = _start;
		self = _self;
		end = _end;
		parentEdge = _parentEdge;
		mapSize = size;
		map = new int[mapSize, mapSize];
		map [0, 0] = 0;
		map [0, mapSize - 1] = 0;
		map [mapSize - 1, 0] = 0;
		map [mapSize - 1, mapSize - 1] = 0;
		RandomCave ();
		if (end) {
			SetTransitionEdge ();
		}
		SmoothCave (smoothAmount);

		CleanUpMap ();

		SmoothCave (1);
		MeshGenerator mesh = GetComponent<MeshGenerator> ();
		mesh.GenerateMesh (map, 1, seed, self);
	}
	//Fills a map with random wall and open areas
	/// <summary>
	/// Randomly fills the cave.
	/// </summary>
	void RandomCave(){
		if (useRandomSeed) {
			int rngSeed = UnityEngine.Random.Range (1, 1000);
			seed = rngSeed.ToString();
		}
		System.Random rng = new System.Random (seed.GetHashCode ());
		for(int x = 0; x < mapSize; x++){
			for( int y = 0; y < mapSize; y ++){
				if((x == 0 && !self.IsChildEdge(3)) || (y == 0 && !self.IsChildEdge(2)) || (x == mapSize - 1 && !self.IsChildEdge(1)) || (y == mapSize - 1 && !self.IsChildEdge(0))){
					map[x,y] = 0;
				}
				else{
					map[x,y] = (rng.Next(0,100)> fillPercent)? 0 : 1;
				}
			}
		}
	}

	//Removes all areas of the map that are boelow the minWallSize and minRoomSize 
	/// <summary>
	/// Cleans up map.
	/// </summary>
	void CleanUpMap (){
		List<Room> finalRooms = new List<Room> ();
		List<List<Tiles>> caveWalls = new List<List<Tiles>> ();
		caveWalls = GetAllAreas (0);
		//Debug.Log ("Outside rooms: " +caveWalls.Count);
		foreach (List<Tiles> walls in caveWalls) {
			if (walls.Count < minWallSize) {
				foreach (Tiles tile in walls) {
					map [tile.x, tile.y] = 1;
				}
			}
		}
		List<List<Tiles>> caveRooms = new List<List<Tiles>> ();
		caveRooms = GetAllAreas (1);
		//Debug.Log ("Inside rooms: " + caveRooms.Count);
		foreach (List<Tiles> rooms in caveRooms) {
			if (rooms.Count < minRoomSize) {
				foreach (Tiles tile in rooms) {
					map [tile.x, tile.y] = 0;
				}
			} else {
				finalRooms.Add (new Room (rooms, map));
			}
		}

		finalRooms.Sort ();
		finalRooms [0].isMainRoom = true;
		finalRooms [0].canReachMainRoom = true;
		CreateHalls (finalRooms);
	}

	/// <summary>
	/// Creates a hall for each room so that they are all connected
	/// </summary>
	/// <param name="_finalRooms">A list of all of the final rooms.</param>
	void CreateHalls (List<Room> _finalRooms, bool forceAccessibility = false){
		List<Room> roomListA = new List<Room> ();
		List<Room> roomListB = new List<Room> ();

		if (forceAccessibility) {
			foreach (Room room in _finalRooms) {
				if (room.canReachMainRoom) {
					roomListB.Add (room);
				} else {
					roomListA.Add (room);
				}
			}
		} else {
			roomListA = _finalRooms;
			roomListB = _finalRooms;
		}
		int closestRoom = 0;
		Tiles bestTileA = new Tiles ();
		Tiles bestTileB = new Tiles ();
		Room bestRoomA = new Room ();
		Room bestRoomB = new Room ();
		bool connectionFound = false;

		foreach (Room roomA in roomListA) {
			if (!forceAccessibility) {
				connectionFound = false;
				if (roomA.connectedRooms.Count > 0) {
					continue;
				}
			}

			foreach (Room roomB in roomListB) {
				if (roomA == roomB || roomA.IsConnected(roomB)) {
					continue;
				}
				for (int indexA = 0; indexA < roomA.edgeTiles.Count; indexA++) {
					for (int indexB = 0; indexB < roomB.edgeTiles.Count; indexB++) {
						Tiles tileA = roomA.edgeTiles [indexA];
						Tiles tileB = roomB.edgeTiles [indexB];
						int distanceBetweenRooms = (int)(Mathf.Pow (tileA.x - tileB.x, 2) + Mathf.Pow (tileA.y - tileB.y, 2));
						if (distanceBetweenRooms < closestRoom || !connectionFound) {
							closestRoom = distanceBetweenRooms;
							connectionFound = true;
							//Debug.Log ("Connection Found!");
							bestTileA = tileA;
							bestTileB = tileB;
							bestRoomA = roomA;
							bestRoomB = roomB;
						}
					}
				}
			}
			if (connectionFound && !forceAccessibility) {
				CreatePassage (bestRoomA, bestRoomB, bestTileA, bestTileB);
			}
		}
		if (connectionFound && forceAccessibility) {
			CreatePassage (bestRoomA, bestRoomB, bestTileA, bestTileB);
			CreateHalls (_finalRooms, true);
		}
		if (!forceAccessibility) {
			CreateHalls (_finalRooms, true);
		}
	}

	///Creates a large hall 
	void CreatePassage(Room roomA, Room roomB, Tiles tileA, Tiles tileB){
		//Debug.Log ("Created Passage at (" + tileA.x + "," + tileA.y + ") (" + tileB.x + "," + tileB.y + ")" );
		Room.ConnectRooms (roomA, roomB);
		List<Tiles> line = GetLine (tileA, tileB);
		foreach (Tiles c in line) {
			DrawCircle (c, 10);
		}

	}
	void DrawCircle(Tiles c, int r){
		for (int x = -r; x <= r; x++) {
			for (int y = -r; y <= r; y++) {
				if (x * x + y * y <= r * r) {
					int realX = c.x + x;
					int realY = c.y + y;
					if(InRange(realX,realY)){
						map[realX, realY] = 1;
					}
				}
			}
		}
	}

	List<Tiles> GetLine(Tiles from, Tiles to){
		List<Tiles> line = new List<Tiles> ();
		int x = from.x;
		int y = from.y;

		int dx = to.x - from.x;
		int dy = to.y - from.y;

		int step = Math.Sign (dx);
		int gradientStep = Math.Sign (dy);

		int longest = Mathf.Abs (dx);
		int shortest = Mathf.Abs (dy);

		bool inverted = false;
		if (longest < shortest) {
			inverted = true;
			longest = Mathf.Abs (dy);
			shortest = Mathf.Abs (dx);
			step = Math.Sign (dy);
			gradientStep = Math.Sign (dx);
		}
		int gradientAccumulation = longest / 2;
		for (int i = 0; i < longest; i++) {
			line.Add (new Tiles (x, y));

			if (inverted) {
				y += step;
			} else {
				x += step;
			}
			gradientAccumulation += shortest;
			if (gradientAccumulation >= longest) {
				if (inverted) {
					x += gradientStep;
				} else {
					y += gradientStep;
				}
				gradientAccumulation -= longest;
			}
		}
		return line;
	}
	///Ensures that the map will have a dead zone around the edges so that the wall are created properly	
	void EnsureEdge(){
		if(!self.IsChildEdge(0)){
			for (int x = 0; x < mapSize; x++) {
				map [x, mapSize - 1] = 0;
			}
		}
		if(!self.IsChildEdge(1)){
			for (int y = 0; y < mapSize; y++) {
				map [mapSize - 1, y] = 0;
			}
		}
		if(!self.IsChildEdge(2)){
			for (int x = 0; x < mapSize; x++) {
				map [x, 0] = 0;
			}
		}
		if(!self.IsChildEdge(3)){
			for (int y = 0; y < mapSize; y++) {
				map [ 0, y] = 0;
			}
		}
	}
		
	//Sets the edge from the parent so the mesh lines up
	/// <summary>
	/// Sets the parent edge.
	/// </summary>
	/// <param name="parent">Parent.</param>
	void SetParentEdge(Edge parent){
		switch (self.parentSide) {
		case 0:
			for (int y = mapSize - 1; y > mapSize - 4; y--) {
				for (int x = 0; x < mapSize; x++) {
					map [x, y] = parent.pattern [x];
				}
			}
			break;
		case 1:
			for (int x = mapSize - 1; x > mapSize - 4; x--) {
				for (int y = 0; y < mapSize; y++) {
					map [x, y] = parent.pattern [y];
				}
			}
			break;
		case 2:
			for (int y = 0; y < 4; y++) {
				for (int x = 0; x < mapSize; x++) {
					map [x, y] = parent.pattern [x];
				}
			}
			break;
		case 3:
			for (int x = 0; x < 4; x++) {
				for (int y = 0; y < mapSize; y++) {
					map [x, y] = parent.pattern [y];
				}
			}
			break;
		}	
	}

	void SetTransitionEdge(){
		int setSide = 4;
		int[] pattern = new int[mapSize];
		int holeStart = UnityEngine.Random.Range (15, mapSize - 15);

		for(int i = 0; i < 4; i++){
			if (self.IsChildEdge (i) && self.parentSide != i) {
				setSide = i;
			}
		}

		Edge endCap = new Edge (setSide, pattern);
		if (self.IsChildEdge (endCap.side)) {
			Debug.Log("The Side is a valid Child edge");
		}
		Debug.Log ("End edge set on zone " + self.identity + " on side: " + endCap.side + ". RNG start = " + holeStart );
		switch (endCap.side) {
		case 0:
			for (int y = mapSize - 1; y > mapSize - 10; y--) {
				for (int x = 0; x < mapSize; x++) {
					if (x >= holeStart && x < holeStart + 8) {
						map [x, y] = 1;
					} else {
						map [x, y] = 0;
					}
				}
			}
			break;
		case 1:
			for (int x = mapSize - 1; x > mapSize - 10; x--) {
				for (int y = 0; y < mapSize; y++) {
					if (y >= holeStart && y < holeStart + 8) {
						map [x, y] = 1;
					} else {
						map [x, y] = 0;
					}
				}
			}
			break;
		case 2:
			for (int y = 0; y < 10; y++) {
				for (int x = 0; x < mapSize; x++) {
					if (x >= holeStart && x < holeStart + 8) {
						map [x, y] = 1;
					} else {
						map [x, y] = 0;
					}
				}
			}
			break;
		case 3:
			for (int x = 0; x < 10; x++) {
				for (int y = 0; y < mapSize; y++) {
					if (y >= holeStart && y < holeStart + 8) {
						map [x, y] = 1;
					} else {
						map [x, y] = 0;
					}
				}
			}
			break;
		}	
	}
	//Runs multiple smoothing iterations so the map will have consistency
	/// <summary>
	/// Smooths the cave by comparing neighbor tiles.
	/// </summary>
	/// <param name="smoothness">Smoothness (Controls how much smoothing to apply).</param>
	void SmoothCave(int smoothness){
		int nearTile;
		for(int i = 0; i < smoothness; i++){
			if (!start) {
				SetParentEdge (parentEdge);
			}
			EnsureEdge ();
			for(int x = 0; x < mapSize; x++){
				for(int y = 0; y < mapSize; y ++){
					bool validSmooth = true;
					nearTile=0;
					if(x + 1 >= mapSize || x - 1 < 0|| y + 1 >= mapSize || y - 1 < 0){
						validSmooth = false;
					}
					if(validSmooth){
						if(map[x+1,y]==1){
							nearTile ++;
						}
						if(map[x-1,y]==1){
							nearTile ++;
						}
						if(map[x,y-1]==1){
							nearTile ++;
						}
						if (map[x,y+1]==1){
							nearTile ++;
						}
						if(map[x+1,y+1]==1){
							nearTile ++;
						}
						if(map[x-1,y-1]==1){
							nearTile ++;
						}
						if(map[x+1,y-1]==1){
							nearTile ++;
						}
						if (map[x-1,y+1]==1){
							nearTile ++;
						}
						if (nearTile > 4){
							map [x, y] = 1;
						}
						if(nearTile < 4){
							map [x, y] = 0;
						}
					}
				}
			}
		}
	}

	//Stores the x and y values of each point in the map for easy access
	struct Tiles{
		public int x;
		public int y;
		public Tiles(int sentX,int sentY){
			x = sentX;
			y = sentY;
		}
	}

	// Returns a list of all tiles of the given type starting at the given location
	List<Tiles> GetArea(int _x, int _y ){
		List<Tiles> roomTiles = new List<Tiles> ();
		int[,] checkedTiles = new int[mapSize, mapSize];
		int tileType = map [_x, _y];

		Queue<Tiles> pendingTiles = new Queue<Tiles> ();
		pendingTiles.Enqueue (new Tiles (_x, _y));
		checkedTiles [_x, _y] = 1;

		while (pendingTiles.Count > 0) {
			Tiles currentTile = pendingTiles.Dequeue ();
			roomTiles.Add (currentTile);
			for (int x = currentTile.x - 1; x <= currentTile.x + 1; x++) {
				for (int y = currentTile.y - 1; y <= currentTile.y + 1; y++) {
					if (InRange (x, y) && (y == currentTile.y || x == currentTile.x)) {
						if (checkedTiles [x, y] == 0 && map [x, y] == tileType) {
							checkedTiles [x, y] = 1;
							pendingTiles.Enqueue (new Tiles (x, y));
						}
					}
				}
			}
		}
		return roomTiles;
	}
	/// <summary>
	/// Gets all areas.
	/// </summary>
	/// <returns>The all areas.</returns>
	/// <param name="tileType">Tile type.</param>
	//Gets all seperate areas in the map to prepare for hall generation
	List<List<Tiles>> GetAllAreas(int tileType){
		List<List<Tiles>> areas = new List<List<Tiles>> ();
		int [,] checkedTile = new int[mapSize,mapSize];

		for(int x = 0; x < mapSize; x++){
			for(int y = 0; y < mapSize; y ++){
				if (checkedTile [x, y] == 0 && map [x, y] == tileType) {
					List<Tiles> newArea = GetArea (x, y);
					areas.Add (newArea);
					foreach (Tiles i in newArea) {
						checkedTile [i.x, i.y] = 1;
					}
				}					
			}
		}
		return areas;
	}

	//Turns map point into world point
	Vector3 CoordToWorldPoint(Tiles tile) {
		return new Vector3 (-mapSize / 2 + .5f + tile.x, 2, -mapSize / 2 + .5f + tile.y);
	}

	/// <summary>
	/// Checs if in the range of map.
	/// </summary>
	/// <returns><c>true</c>, if range was ined, <c>false</c> otherwise.</returns>
	/// <param name="x">The x coordinate.</param>
	/// <param name="y">The y coordinate.</param>
	bool InRange(int x, int y){
		return x >= 0 && x < mapSize && y >= 0 && y < mapSize;
	}
	/// <summary>
	/// Gets the edge.
	/// </summary>
	/// <returns>The edge.</returns>
	/// <param name="_side">Side.</param>
	public int[] GetEdge(int _side){
		int[] top = new int[mapSize];
		int[] left = new int[mapSize];;
		int[] bottom = new int[mapSize];;
		int[] right = new int[mapSize];;

		switch (_side) {
		case 2:
			for (int x = 0; x < mapSize; x++) {
				bottom [x] = map [x, 0]; 
			}
			return bottom;
		case 0:
			for (int x = 0; x < mapSize; x++) {
				top [x] = map [x, mapSize]; 
			}
			return top;
		case 3:
			for (int y = 0; y < mapSize; y++) {
				left [y] = map [0, y]; 
			}
			return left;
		case 1:
			for (int y = 0; y < mapSize; y++) {
				right [y] = map [mapSize, y]; 
			}
			return right;
		}
		return new int[0];
	}
	/// <summary>
	/// Stores all of the variables for the room
	/// </summary>
	class Room: IComparable<Room>{
		public List<Tiles> roomTiles;
		public List<Tiles> edgeTiles;
		public List<Room> connectedRooms;
		public int roomSize;
		public bool canReachMainRoom;
		public bool isMainRoom;
		public int[,] map;
		public Room(){
		}
		public Room(List<Tiles> tiles, int[,] _map){
			map = _map;
			roomTiles = tiles;
			roomSize = tiles.Count;
			connectedRooms = new List<Room>();
			edgeTiles = new List<Tiles>();
			foreach(Tiles tile in roomTiles){
				for(int x = tile.x - 1 ; x <= tile.x + 1; x++){
					for(int y = tile.y -1 ; y <= tile.y + 1; y++){
						if(x == tile.x || y == tile.y){
							if(!IsInRange(x,y)){
								edgeTiles.Add(tile);
							}
							else if(map[x,y] == 0){
								edgeTiles.Add(tile);
							}
						}
					}
				}
			}
			//Debug.Log(edgeTiles.Count);
		}
		public bool IsInRange(int x, int y){
			return x >= 0 && x < map.GetLength(0) && y >= 0 && y < map.GetLength(1);					
		}
		public void SetAccessibleFromMainRoom(){
			if(!canReachMainRoom){
				canReachMainRoom = true;
				foreach(Room connectedRoom in connectedRooms){
					connectedRoom.SetAccessibleFromMainRoom ();
				}
			}
		}
		public static void ConnectRooms(Room roomA, Room roomB){
			if (roomA.canReachMainRoom) {
				roomB.SetAccessibleFromMainRoom ();
			}else if(roomB.canReachMainRoom){
				roomA.SetAccessibleFromMainRoom();
			}
			roomA.connectedRooms.Add (roomB);
			roomB.connectedRooms.Add (roomA);
		}
		public bool IsConnected(Room otherRoom){
			return connectedRooms.Contains (otherRoom);
		}
		public int CompareTo(Room otherRoom) {
			return otherRoom.roomSize.CompareTo (roomSize);
		}
	}
}
	

	

