// MongoDB Initialization Script for DM3
// This script runs automatically on first container startup
// Index conventions: docs/conventions/DATA_STORAGE.md
//
// Mongo runs /docker-entrypoint-initdb.d exactly once, when the volume is empty, so
// nothing written here can ever reach a database that already exists. That leaves this
// script the one job it alone can do: creating the application user, which needs the root
// account the application itself never holds.
//
// Indexes are not declared here. They were, as a second copy of the whole set, and a copy
// that can only ever be applied to a brand-new volume drifts in one direction and is
// noticed by nobody. MongoIndexInitializer asserts the set on every startup of the API and
// of the seeder, and that covers the fresh volume as well.

// Switch to the application database
db = db.getSiblingDB('dm3');

// Least-privilege application user. The root account exists only to bootstrap
// this one and to run health checks; the application never uses it. readWrite on
// dm3 is enough — it covers index creation, which MongoIndexInitializer performs
// on every startup.
const appUser = process.env.DM_MONGO_USER;
const appPassword = process.env.DM_MONGO_PASSWORD;
if (!appUser || !appPassword) {
    throw new Error('DM_MONGO_USER and DM_MONGO_PASSWORD must be set: refusing to leave dm3 without an application user');
}
db.createUser({
    user: appUser,
    pwd: appPassword,
    roles: [{ role: 'readWrite', db: 'dm3' }]
});
print('Application user created: ' + appUser);

print('Initialization complete: MongoIndexInitializer asserts the indexes on startup');
