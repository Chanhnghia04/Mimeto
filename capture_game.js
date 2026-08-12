const { execSync } = require('child_process');
const fs = require('fs');
try {
    const out = execSync('unity-mcp-cli run-tool screenshot-game-view --input "{}" --raw', { maxBuffer: 1024 * 1024 * 50 });
    const jsonStr = out.toString();
    const result = JSON.parse(jsonStr);
    
    // Find image in content
    if (result.content) {
        for (const item of result.content) {
            if (item.type === 'image' && item.data) {
                fs.writeFileSync('screenshot_game.png', Buffer.from(item.data, 'base64'));
                console.log('SUCCESS');
                process.exit(0);
            }
        }
    }
    console.log('NO IMAGE FOUND', jsonStr.substring(0, 200));
} catch (e) {
    console.error(e.toString());
}
