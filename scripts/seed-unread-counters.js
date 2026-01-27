// Script to seed UnreadCounters collection in MongoDB
// Run with: docker exec dm-mongo mongosh "dm3-5" /scripts/seed-unread-counters.js

// Helper to convert bytes array to Base64
function bytesToBase64(bytes) {
  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';
  let result = '';
  for (let i = 0; i < bytes.length; i += 3) {
    const a = bytes[i];
    const b = bytes[i + 1] || 0;
    const c = bytes[i + 2] || 0;
    result += chars[(a >> 2)];
    result += chars[((a & 3) << 4) | (b >> 4)];
    result += (i + 1 < bytes.length) ? chars[((b & 15) << 2) | (c >> 6)] : '=';
    result += (i + 2 < bytes.length) ? chars[(c & 63)] : '=';
  }
  return result;
}

// Helper to create CSUUID (CSharpLegacy format, subtype 3) from UUID string
// C# driver uses different byte order for first 3 components (little-endian)
function csuuidFromString(uuidString) {
  // Parse UUID string: aabbccdd-eeff-0011-2233-445566778899
  const hex = uuidString.replace(/-/g, '');

  // Standard order: time-low(8), time-mid(4), time-hi(4), clock-seq(4), node(12)
  // CSharpLegacy swaps bytes in first 3 groups
  const bytes = [];

  // time-low (4 bytes, reversed)
  bytes.push(parseInt(hex.substring(6, 8), 16));
  bytes.push(parseInt(hex.substring(4, 6), 16));
  bytes.push(parseInt(hex.substring(2, 4), 16));
  bytes.push(parseInt(hex.substring(0, 2), 16));

  // time-mid (2 bytes, reversed)
  bytes.push(parseInt(hex.substring(10, 12), 16));
  bytes.push(parseInt(hex.substring(8, 10), 16));

  // time-hi-and-version (2 bytes, reversed)
  bytes.push(parseInt(hex.substring(14, 16), 16));
  bytes.push(parseInt(hex.substring(12, 14), 16));

  // clock-seq and node (8 bytes, not reversed)
  for (let i = 16; i < 32; i += 2) {
    bytes.push(parseInt(hex.substring(i, i + 2), 16));
  }

  // Create Base64 from bytes
  const base64 = bytesToBase64(bytes);

  // Return BinData with subtype 3 (CSUUID)
  return BinData(3, base64);
}

// Alias for clarity
function uuidToBinary(uuidString) {
  return csuuidFromString(uuidString);
}

// Game rooms with post counts (from PostgreSQL)
const gameRooms = [
  { roomId: "20000000-0000-0000-0000-000000000002", gameId: "10000000-0000-0000-0000-000000000001", posts: 2 },
  { roomId: "20000000-0000-0000-0000-000000000001", gameId: "10000000-0000-0000-0000-000000000001", posts: 3 },
  { roomId: "8aa79c2b-4207-4b0a-b8bd-a22fc6785502", gameId: "ad2eb425-9cdf-4c9e-832d-b993f828352c", posts: 0 },
  { roomId: "ecc993f3-44c1-4384-a20a-9f2d3b418fc9", gameId: "ad2eb425-9cdf-4c9e-832d-b993f828352c", posts: 2 },
  { roomId: "cd477072-0d44-4257-8f5f-5915a1bc1522", gameId: "ad2eb425-9cdf-4c9e-832d-b993f828352c", posts: 0 },
  { roomId: "7baf8f7e-c21c-420c-96ed-5056f1f2f141", gameId: "ade4cdba-10b5-48aa-a1fd-523bdf5a22c9", posts: 0 },
  { roomId: "5cbffb67-1e9c-4bee-b735-aaf1a38bc86e", gameId: "ade4cdba-10b5-48aa-a1fd-523bdf5a22c9", posts: 0 },
  { roomId: "0c069321-3725-48a8-9cf8-2d6ea7212db6", gameId: "ade4cdba-10b5-48aa-a1fd-523bdf5a22c9", posts: 2 },
  { roomId: "8de7d352-5c23-42cc-a913-f9e8bc8ffa9b", gameId: "b11188eb-ab4e-436a-8fd4-595fd58ace2b", posts: 0 },
  { roomId: "9ec0702a-e101-4166-953a-de02a2214273", gameId: "b11188eb-ab4e-436a-8fd4-595fd58ace2b", posts: 2 },
  { roomId: "c97c77b2-3330-4a2d-a095-26bfa5ffc121", gameId: "b11188eb-ab4e-436a-8fd4-595fd58ace2b", posts: 0 },
  { roomId: "8dd9b8f5-54fe-4e30-91f6-4b11c9901625", gameId: "ba956e31-e6d5-42f5-89a2-69520b929ac1", posts: 2 },
  { roomId: "8c3582f8-51cf-407f-a675-8e42be03308d", gameId: "ba956e31-e6d5-42f5-89a2-69520b929ac1", posts: 0 },
  { roomId: "5bfefa5b-a82d-41a1-bac3-2bc4a9899114", gameId: "ba956e31-e6d5-42f5-89a2-69520b929ac1", posts: 0 },
  { roomId: "49408f88-c7cc-49ca-99bb-82eac0344dc4", gameId: "e0cf8992-75c4-4ef4-91af-d0196c482016", posts: 0 },
  { roomId: "0261cf5a-c92f-4e52-8380-7ec90d48acb7", gameId: "e0cf8992-75c4-4ef4-91af-d0196c482016", posts: 2 },
  { roomId: "546b1431-f9f0-4523-9c2c-aee8ee97ad19", gameId: "e0cf8992-75c4-4ef4-91af-d0196c482016", posts: 0 },
  { roomId: "f29eaaf6-8aeb-47d2-b31b-69ff5aeeb651", gameId: "e75c1a04-598b-4163-835b-648515c7f1bf", posts: 2 },
  { roomId: "7f15ff8d-f11f-4e8c-8829-ad6ab4d70c6d", gameId: "e75c1a04-598b-4163-835b-648515c7f1bf", posts: 0 },
  { roomId: "6e23087e-4b06-4e72-a3af-47738a464a7b", gameId: "e75c1a04-598b-4163-835b-648515c7f1bf", posts: 0 },
];

// Forum topics with comment counts (from PostgreSQL)
const forumTopics = [
  { topicId: "c0000000-0000-0000-0000-000000000011", forumId: "00000000-0000-0000-0000-000000000000", comments: 2 },
  { topicId: "f0000001-0001-0001-0001-000000000001", forumId: "00000000-0000-0000-0000-000000000000", comments: 2 },
  { topicId: "ec833ad0-a969-4cc7-aeee-c7dfefcf0e90", forumId: "00000000-0000-0000-0000-000000000000", comments: 2 },
  { topicId: "ccc084c7-ba0e-4241-81df-04f04451777f", forumId: "00000000-0000-0000-0000-000000000000", comments: 0 },
  { topicId: "c0000000-0000-0000-0000-000000000012", forumId: "00000000-0000-0000-0000-000000000000", comments: 0 },
  { topicId: "c0000000-0000-0000-0000-000000000010", forumId: "00000000-0000-0000-0000-000000000000", comments: 0 },
  { topicId: "924a970a-471e-4222-b5fe-ea5ea38f56e0", forumId: "00000000-0000-0000-0000-000000000000", comments: 1 },
  { topicId: "f0000001-0002-0001-0001-000000000001", forumId: "00000000-0000-0000-0000-000000000001", comments: 1 },
  { topicId: "70094b7e-85e3-4d01-9f5f-c2562908b55b", forumId: "00000000-0000-0000-0000-000000000001", comments: 0 },
  { topicId: "923b059b-6abc-478c-b57a-b1c9bc049d4f", forumId: "00000000-0000-0000-0000-000000000001", comments: 2 },
  { topicId: "f0000001-0003-0001-0001-000000000001", forumId: "00000000-0000-0000-0000-000000000002", comments: 1 },
  { topicId: "a6ef4937-c58d-4ecf-9614-733dd71c16ee", forumId: "00000000-0000-0000-0000-000000000002", comments: 1 },
  { topicId: "51a8f07a-484c-453a-9f3d-5d2eb55e3064", forumId: "00000000-0000-0000-0000-000000000003", comments: 0 },
  { topicId: "f0000001-0004-0001-0001-000000000001", forumId: "00000000-0000-0000-0000-000000000003", comments: 1 },
  { topicId: "f0000001-0005-0001-0001-000000000001", forumId: "00000000-0000-0000-0000-000000000004", comments: 1 },
  { topicId: "61698ece-2c4c-4f3e-b3e0-b72d36ad51e5", forumId: "00000000-0000-0000-0000-000000000004", comments: 2 },
  { topicId: "f0000001-0007-0001-0001-000000000001", forumId: "00000000-0000-0000-0000-000000000005", comments: 1 },
  { topicId: "a0779668-42af-48b3-ac22-39bed3773a7d", forumId: "00000000-0000-0000-0000-000000000005", comments: 1 },
  { topicId: "e5aa68be-9231-4508-9e3d-d85c83dccc8d", forumId: "00000000-0000-0000-0000-000000000007", comments: 1 },
  { topicId: "f0000001-0008-0001-0001-000000000001", forumId: "00000000-0000-0000-0000-000000000007", comments: 1 },
  { topicId: "c0000000-0000-0000-0000-000000000002", forumId: "00000000-0000-0000-0000-000000000008", comments: 0 },
  { topicId: "c0000000-0000-0000-0000-000000000001", forumId: "00000000-0000-0000-0000-000000000008", comments: 0 },
  { topicId: "f0000001-0009-0001-0001-000000000001", forumId: "00000000-0000-0000-0000-000000000008", comments: 1 },
  { topicId: "c0000000-0000-0000-0000-000000000003", forumId: "00000000-0000-0000-0000-000000000008", comments: 0 },
  { topicId: "f0000001-0010-0001-0001-000000000001", forumId: "00000000-0000-0000-0000-000000000010", comments: 1 },
  { topicId: "5e5e448a-eb1c-4939-81b8-dc178da734c7", forumId: "00000000-0000-0000-0000-000000000010", comments: 0 },
  { topicId: "a7d00de8-85fc-4e22-8068-70901fb806c4", forumId: "00000000-0000-0000-0000-000000000011", comments: 0 },
  { topicId: "f0000001-0011-0001-0001-000000000001", forumId: "00000000-0000-0000-0000-000000000011", comments: 1 },
];

// Empty GUID for anonymous/global counters
const emptyGuid = "00000000-0000-0000-0000-000000000000";

// Current timestamp
const now = new Date();

// EntryType enum: Message = 0, Character = 1
const EntryType = {
  Message: 0,
  Character: 1
};

print("Clearing existing UnreadCounters...");
db.UnreadCounters.deleteMany({});

print("Creating UnreadCounters for forum topics...");
const forumDocs = forumTopics.map(topic => ({
  UserId: uuidToBinary(emptyGuid),
  EntityId: uuidToBinary(topic.topicId),
  ParentId: uuidToBinary(topic.forumId),
  EntryType: EntryType.Message,
  LastRead: now,
  Counter: topic.comments,
  IsRemoved: false
}));

if (forumDocs.length > 0) {
  db.UnreadCounters.insertMany(forumDocs);
  print(`Inserted ${forumDocs.length} forum topic counters`);
}

print("Creating UnreadCounters for game rooms...");
const roomDocs = gameRooms.map(room => ({
  UserId: uuidToBinary(emptyGuid),
  EntityId: uuidToBinary(room.roomId),
  ParentId: uuidToBinary(room.roomId), // For rooms, ParentId = EntityId (room is self-contained)
  EntryType: EntryType.Message,
  LastRead: now,
  Counter: room.posts,
  IsRemoved: false
}));

if (roomDocs.length > 0) {
  db.UnreadCounters.insertMany(roomDocs);
  print(`Inserted ${roomDocs.length} game room counters`);
}

print("\nDone! Total documents: " + db.UnreadCounters.countDocuments());
print("\nSample forum topic counters:");
db.UnreadCounters.find({ ParentId: { $ne: db.UnreadCounters.findOne().EntityId } }).limit(3).forEach(doc => printjson(doc));
print("\nSample room counters:");
db.UnreadCounters.find().limit(3).forEach(doc => printjson(doc));
