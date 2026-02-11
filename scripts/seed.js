/**
 * Seed script for DM3
 *
 * Creates test data:
 * - Users (all roles + special states)
 *
 * Usage: node scripts/seed.js
 * Requires: API running on http://localhost:5000
 */

const API_BASE = process.env.API_URL || 'http://localhost:5000';

async function main() {
    console.log('DM3 Seed Script');
    console.log('================\n');
    console.log('API:', API_BASE);
    console.log('');

    // Call the seed endpoint
    console.log('Calling seed endpoint...\n');

    try {
        const response = await fetch(`${API_BASE}/v1/moderation/seed`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
        });

        if (!response.ok) {
            if (response.status === 404) {
                console.error('Error: Seed endpoint not available.');
                console.error('This endpoint is only available in development environment.');
                console.error('Make sure ASPNETCORE_ENVIRONMENT=Development');
                process.exit(1);
            }
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const result = await response.json();

        console.log('Seed Results:');
        console.log('=============');
        console.log(`Created: ${result.created}`);
        console.log(`Skipped: ${result.skipped} (already exist)`);
        console.log('');

        if (result.createdLogins && result.createdLogins.length > 0) {
            console.log('Created users:');
            result.createdLogins.forEach(login => {
                console.log(`  + ${login}`);
            });
            console.log('');
        }

        if (result.skippedLogins && result.skippedLogins.length > 0) {
            console.log('Skipped users (already exist):');
            result.skippedLogins.forEach(login => {
                console.log(`  - ${login}`);
            });
            console.log('');
        }

        console.log('All test users have password: Test123!');
        console.log('All users start with 0 posts (newbie status)');
        console.log('');
        console.log('Test accounts:');
        console.log('  TestAdmin       - Admin');
        console.log('  TestSeniorMod   - SeniorModerator');
        console.log('  TestModerator   - Moderator');
        console.log('  TestMentor      - Mentor');
        console.log('  TestUser        - RegularUser');
        console.log('  Ab              - RegularUser (min login length)');
        console.log('  LongestLoginPossible - RegularUser (max login length)');
        console.log('  Player_One      - RegularUser (with underscore)');
        console.log('  Player-Two      - RegularUser (with hyphen)');
        console.log('  TestHonorary    - RegularUser (honorary goblin)');
        console.log('');
        console.log('Pending registration (for testing activation flow):');
        console.log('  inactive@test.local - not activated, no login yet');

    } catch (err) {
        console.error('Seed failed:', err.message);
        process.exit(1);
    }
}

main();
