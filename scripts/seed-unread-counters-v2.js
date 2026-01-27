// Script to seed UnreadCounters collection in MongoDB
// Run with: docker exec dm-mongo mongosh "dm3-5" /scripts/seed-unread-counters-v2.js
//
// RULE: For user X in place Y (topic/room):
// - Read = their content + everything BEFORE their last content in place Y
// - Unread = content AFTER their last content in Y (from others)

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

// Helper to create CSUUID (CSharpLegacy format, subtype 3)
function csuuid(uuidString) {
  const hex = uuidString.replace(/-/g, '');
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
  return BinData(3, bytesToBase64(bytes));
}

const emptyGuid = "00000000-0000-0000-0000-000000000000";
const now = new Date();
const EntryType = { Message: 0, Character: 1 };

// ============ DATA FROM POSTGRESQL ============

// Users
const users = {
  "a0000000-0000-0000-0000-000000000001": "TestUser",
  "398a0dab-9b18-48bf-a299-1f18d8338bf4": "SolohinLex",
  "c3b60452-efd3-4b8d-9228-590beef59f66": "Rayzen",
  "fffb4259-795c-45e8-82d2-7b53e31f3309": "Akkarin",
};

// Topics: topicId -> { forumId, authorId }
const topics = {
  "c0000000-0000-0000-0000-000000000001": { forumId: "00000000-0000-0000-0000-000000000008", authorId: "a0000000-0000-0000-0000-000000000001" },
  "c0000000-0000-0000-0000-000000000002": { forumId: "00000000-0000-0000-0000-000000000008", authorId: "a0000000-0000-0000-0000-000000000001" },
  "c0000000-0000-0000-0000-000000000003": { forumId: "00000000-0000-0000-0000-000000000008", authorId: "a0000000-0000-0000-0000-000000000001" },
  "c0000000-0000-0000-0000-000000000010": { forumId: "00000000-0000-0000-0000-000000000000", authorId: "a0000000-0000-0000-0000-000000000001" },
  "c0000000-0000-0000-0000-000000000011": { forumId: "00000000-0000-0000-0000-000000000000", authorId: "a0000000-0000-0000-0000-000000000001" },
  "c0000000-0000-0000-0000-000000000012": { forumId: "00000000-0000-0000-0000-000000000000", authorId: "a0000000-0000-0000-0000-000000000001" },
  "f0000001-0001-0001-0001-000000000001": { forumId: "00000000-0000-0000-0000-000000000000", authorId: "a0000000-0000-0000-0000-000000000001" },
  "f0000001-0002-0001-0001-000000000001": { forumId: "00000000-0000-0000-0000-000000000001", authorId: "a0000000-0000-0000-0000-000000000001" },
  "f0000001-0003-0001-0001-000000000001": { forumId: "00000000-0000-0000-0000-000000000002", authorId: "a0000000-0000-0000-0000-000000000001" },
  "f0000001-0004-0001-0001-000000000001": { forumId: "00000000-0000-0000-0000-000000000003", authorId: "a0000000-0000-0000-0000-000000000001" },
  "f0000001-0005-0001-0001-000000000001": { forumId: "00000000-0000-0000-0000-000000000004", authorId: "a0000000-0000-0000-0000-000000000001" },
  "f0000001-0007-0001-0001-000000000001": { forumId: "00000000-0000-0000-0000-000000000005", authorId: "a0000000-0000-0000-0000-000000000001" },
  "f0000001-0008-0001-0001-000000000001": { forumId: "00000000-0000-0000-0000-000000000007", authorId: "a0000000-0000-0000-0000-000000000001" },
  "f0000001-0009-0001-0001-000000000001": { forumId: "00000000-0000-0000-0000-000000000008", authorId: "a0000000-0000-0000-0000-000000000001" },
  "f0000001-0010-0001-0001-000000000001": { forumId: "00000000-0000-0000-0000-000000000010", authorId: "a0000000-0000-0000-0000-000000000001" },
  "f0000001-0011-0001-0001-000000000001": { forumId: "00000000-0000-0000-0000-000000000011", authorId: "a0000000-0000-0000-0000-000000000001" },
  "ccc084c7-ba0e-4241-81df-04f04451777f": { forumId: "00000000-0000-0000-0000-000000000000", authorId: "a0000000-0000-0000-0000-000000000001" },
  "ec833ad0-a969-4cc7-aeee-c7dfefcf0e90": { forumId: "00000000-0000-0000-0000-000000000000", authorId: "a0000000-0000-0000-0000-000000000001" },
  "924a970a-471e-4222-b5fe-ea5ea38f56e0": { forumId: "00000000-0000-0000-0000-000000000000", authorId: "a0000000-0000-0000-0000-000000000001" },
  "923b059b-6abc-478c-b57a-b1c9bc049d4f": { forumId: "00000000-0000-0000-0000-000000000001", authorId: "a0000000-0000-0000-0000-000000000001" },
  "70094b7e-85e3-4d01-9f5f-c2562908b55b": { forumId: "00000000-0000-0000-0000-000000000001", authorId: "a0000000-0000-0000-0000-000000000001" },
  "a6ef4937-c58d-4ecf-9614-733dd71c16ee": { forumId: "00000000-0000-0000-0000-000000000002", authorId: "a0000000-0000-0000-0000-000000000001" },
  "51a8f07a-484c-453a-9f3d-5d2eb55e3064": { forumId: "00000000-0000-0000-0000-000000000003", authorId: "a0000000-0000-0000-0000-000000000001" },
  "61698ece-2c4c-4f3e-b3e0-b72d36ad51e5": { forumId: "00000000-0000-0000-0000-000000000004", authorId: "a0000000-0000-0000-0000-000000000001" },
  "a0779668-42af-48b3-ac22-39bed3773a7d": { forumId: "00000000-0000-0000-0000-000000000005", authorId: "a0000000-0000-0000-0000-000000000001" },
  "5e5e448a-eb1c-4939-81b8-dc178da734c7": { forumId: "00000000-0000-0000-0000-000000000010", authorId: "a0000000-0000-0000-0000-000000000001" },
  "a7d00de8-85fc-4e22-8068-70901fb806c4": { forumId: "00000000-0000-0000-0000-000000000011", authorId: "a0000000-0000-0000-0000-000000000001" },
  "e5aa68be-9231-4508-9e3d-d85c83dccc8d": { forumId: "00000000-0000-0000-0000-000000000007", authorId: "a0000000-0000-0000-0000-000000000001" },
};

// Comments grouped by topic: topicId -> [{ authorId, date }]
const commentsByTopic = {
  "c0000000-0000-0000-0000-000000000011": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "f0000001-0001-0001-0001-000000000001": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "ec833ad0-a969-4cc7-aeee-c7dfefcf0e90": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "924a970a-471e-4222-b5fe-ea5ea38f56e0": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "923b059b-6abc-478c-b57a-b1c9bc049d4f": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "f0000001-0002-0001-0001-000000000001": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "f0000001-0003-0001-0001-000000000001": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "a6ef4937-c58d-4ecf-9614-733dd71c16ee": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "f0000001-0004-0001-0001-000000000001": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "f0000001-0005-0001-0001-000000000001": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "61698ece-2c4c-4f3e-b3e0-b72d36ad51e5": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "f0000001-0007-0001-0001-000000000001": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "a0779668-42af-48b3-ac22-39bed3773a7d": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "f0000001-0008-0001-0001-000000000001": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "e5aa68be-9231-4508-9e3d-d85c83dccc8d": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "f0000001-0009-0001-0001-000000000001": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "f0000001-0010-0001-0001-000000000001": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "f0000001-0011-0001-0001-000000000001": [
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
};

// Posts grouped by room: roomId -> [{ authorId }]
// Data from PostgreSQL query - 8 rooms with posts, 17 total posts
const postsByRoom = {
  // Хроники Забытых Королевств - TestUser (5 posts in 2 rooms)
  "20000000-0000-0000-0000-000000000001": [
    { authorId: "a0000000-0000-0000-0000-000000000001" }, // TestUser
    { authorId: "a0000000-0000-0000-0000-000000000001" },
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  "20000000-0000-0000-0000-000000000002": [
    { authorId: "a0000000-0000-0000-0000-000000000001" }, // TestUser
    { authorId: "a0000000-0000-0000-0000-000000000001" },
  ],
  // Тени над Вестмаром - SolohinLex (4 posts in 2 rooms)
  "0261cf5a-c92f-4e52-8380-7ec90d48acb7": [
    { authorId: "398a0dab-9b18-48bf-a299-1f18d8338bf4" }, // SolohinLex
    { authorId: "398a0dab-9b18-48bf-a299-1f18d8338bf4" },
  ],
  "0c069321-3725-48a8-9cf8-2d6ea7212db6": [
    { authorId: "398a0dab-9b18-48bf-a299-1f18d8338bf4" }, // SolohinLex
    { authorId: "398a0dab-9b18-48bf-a299-1f18d8338bf4" },
  ],
  // Звёздные Рубежи - Rayzen (4 posts in 2 rooms)
  "9ec0702a-e101-4166-953a-de02a2214273": [
    { authorId: "c3b60452-efd3-4b8d-9228-590beef59f66" }, // Rayzen
    { authorId: "c3b60452-efd3-4b8d-9228-590beef59f66" },
  ],
  "f29eaaf6-8aeb-47d2-b31b-69ff5aeeb651": [
    { authorId: "c3b60452-efd3-4b8d-9228-590beef59f66" }, // Rayzen
    { authorId: "c3b60452-efd3-4b8d-9228-590beef59f66" },
  ],
  // Хроники Кровавой Луны - Akkarin (4 posts in 2 rooms)
  "8dd9b8f5-54fe-4e30-91f6-4b11c9901625": [
    { authorId: "fffb4259-795c-45e8-82d2-7b53e31f3309" }, // Akkarin
    { authorId: "fffb4259-795c-45e8-82d2-7b53e31f3309" },
  ],
  "ecc993f3-44c1-4384-a20a-9f2d3b418fc9": [
    { authorId: "fffb4259-795c-45e8-82d2-7b53e31f3309" }, // Akkarin
    { authorId: "fffb4259-795c-45e8-82d2-7b53e31f3309" },
  ],
};

// Rooms: roomId -> gameId (for empty rooms)
const rooms = {
  "20000000-0000-0000-0000-000000000001": "10000000-0000-0000-0000-000000000001",
  "20000000-0000-0000-0000-000000000002": "10000000-0000-0000-0000-000000000001",
  "49408f88-c7cc-49ca-99bb-82eac0344dc4": "e0cf8992-75c4-4ef4-91af-d0196c482016",
  "546b1431-f9f0-4523-9c2c-aee8ee97ad19": "e0cf8992-75c4-4ef4-91af-d0196c482016",
  "0261cf5a-c92f-4e52-8380-7ec90d48acb7": "e0cf8992-75c4-4ef4-91af-d0196c482016",
  "6e23087e-4b06-4e72-a3af-47738a464a7b": "e75c1a04-598b-4163-835b-648515c7f1bf",
  "7f15ff8d-f11f-4e8c-8829-ad6ab4d70c6d": "e75c1a04-598b-4163-835b-648515c7f1bf",
  "f29eaaf6-8aeb-47d2-b31b-69ff5aeeb651": "e75c1a04-598b-4163-835b-648515c7f1bf",
  "8c3582f8-51cf-407f-a675-8e42be03308d": "ba956e31-e6d5-42f5-89a2-69520b929ac1",
  "5bfefa5b-a82d-41a1-bac3-2bc4a9899114": "ba956e31-e6d5-42f5-89a2-69520b929ac1",
  "8dd9b8f5-54fe-4e30-91f6-4b11c9901625": "ba956e31-e6d5-42f5-89a2-69520b929ac1",
  "5cbffb67-1e9c-4bee-b735-aaf1a38bc86e": "ade4cdba-10b5-48aa-a1fd-523bdf5a22c9",
  "7baf8f7e-c21c-420c-96ed-5056f1f2f141": "ade4cdba-10b5-48aa-a1fd-523bdf5a22c9",
  "0c069321-3725-48a8-9cf8-2d6ea7212db6": "ade4cdba-10b5-48aa-a1fd-523bdf5a22c9",
  "8de7d352-5c23-42cc-a913-f9e8bc8ffa9b": "b11188eb-ab4e-436a-8fd4-595fd58ace2b",
  "c97c77b2-3330-4a2d-a095-26bfa5ffc121": "b11188eb-ab4e-436a-8fd4-595fd58ace2b",
  "9ec0702a-e101-4166-953a-de02a2214273": "b11188eb-ab4e-436a-8fd4-595fd58ace2b",
  "cd477072-0d44-4257-8f5f-5915a1bc1522": "ad2eb425-9cdf-4c9e-832d-b993f828352c",
  "8aa79c2b-4207-4b0a-b8bd-a22fc6785502": "ad2eb425-9cdf-4c9e-832d-b993f828352c",
  "ecc993f3-44c1-4384-a20a-9f2d3b418fc9": "ad2eb425-9cdf-4c9e-832d-b993f828352c",
};

// ============ BUILD COUNTERS ============

print("Clearing existing UnreadCounters...");
db.UnreadCounters.deleteMany({});

const docs = [];

// 1. FORUM TOPICS - counter for topics themselves (each topic = 1 unread item)
// For topics, EntityId = topicId, ParentId = forumId
// Counter represents number of comments in topic
print("Creating counters for forum topics...");

for (const [topicId, topic] of Object.entries(topics)) {
  const comments = commentsByTopic[topicId] || [];
  const commentCount = comments.length;

  // GLOBAL counter - total comments in this topic
  docs.push({
    UserId: csuuid(emptyGuid),
    EntityId: csuuid(topicId),
    ParentId: csuuid(topic.forumId),
    EntryType: EntryType.Message,
    LastRead: now,
    Counter: commentCount,
    IsRemoved: false
  });

  // USER-SPECIFIC counter for topic author (TestUser wrote all)
  // Since TestUser wrote ALL comments in ALL topics, his counter = 0
  docs.push({
    UserId: csuuid(topic.authorId),
    EntityId: csuuid(topicId),
    ParentId: csuuid(topic.forumId),
    EntryType: EntryType.Message,
    LastRead: now,
    Counter: 0, // Author sees 0 unread
    IsRemoved: false
  });
}

// 2. GAME ROOMS - counter for posts in rooms
print("Creating counters for game rooms...");

for (const [roomId, gameId] of Object.entries(rooms)) {
  const posts = postsByRoom[roomId] || [];
  const postCount = posts.length;

  // GLOBAL counter - total posts in this room
  docs.push({
    UserId: csuuid(emptyGuid),
    EntityId: csuuid(roomId),
    ParentId: csuuid(roomId), // For rooms, ParentId = EntityId
    EntryType: EntryType.Message,
    LastRead: now,
    Counter: postCount,
    IsRemoved: false
  });

  // Find unique authors in this room and create user-specific counters
  if (posts.length > 0) {
    const authorIds = [...new Set(posts.map(p => p.authorId))];

    for (const authorId of authorIds) {
      // Count posts AFTER author's last post in this room
      // Since all posts in each room are by same author, they see 0 unread
      docs.push({
        UserId: csuuid(authorId),
        EntityId: csuuid(roomId),
        ParentId: csuuid(roomId),
        EntryType: EntryType.Message,
        LastRead: now,
        Counter: 0, // Author sees 0 unread in their room
        IsRemoved: false
      });
    }
  }
}

// Insert all documents
print("Inserting " + docs.length + " documents...");
db.UnreadCounters.insertMany(docs);

// Summary
print("\n=== SUMMARY ===");
print("Total documents: " + db.UnreadCounters.countDocuments());
print("Global counters: " + db.UnreadCounters.countDocuments({UserId: csuuid(emptyGuid)}));
print("User-specific counters: " + db.UnreadCounters.countDocuments({UserId: {$ne: csuuid(emptyGuid)}}));

print("\n=== EXPECTED RESULTS ===");
print("TestUser (wrote all forum content + 5 game posts):");
print("  - Forum topics with comments: 0 unread (wrote all comments)");
print("  - Game posts: 12 unread (17 total - 5 his own)");
print("");
print("SolohinLex (wrote 4 game posts in 2 rooms):");
print("  - Forum: 18 topics with comments unread (didn't write any)");
print("  - Game posts: 13 unread (17 total - 4 his own)");
print("");
print("Rayzen (wrote 4 game posts in 2 rooms):");
print("  - Forum: 18 topics with comments unread");
print("  - Game posts: 13 unread");
print("");
print("Akkarin (wrote 4 game posts in 2 rooms):");
print("  - Forum: 18 topics with comments unread");
print("  - Game posts: 13 unread");
print("");
print("Others (no activity):");
print("  - Forum: 18 topics with comments unread");
print("  - Game posts: 17 unread");
