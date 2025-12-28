let player;
let token;

window.onSpotifyWebPlaybackSDKReady = () => {
    console.log('SDK is ready');
    initialize();
}

async function initialize() {

    await fetchToken();

    if (token) {
        initializePlayer();
    } else {
        document.getElementById('status').innerText = 'Failed to get token';
    }
}

async function fetchToken() {
    try {
        const response = await fetch('/api/token');
        const data = await response.json();
        token = data.token;

        if (!token || token === "") {
            console.error('Token is empty!');
            document.getElementById('status').innerText = 'No token received from server';
        } else {
            console.log('Got token:', token.substring(0, 20) + '...');
        }
    } catch (error) {
        console.error('Error fetching token:', error);
    }
}

function initializePlayer() {
    console.log('Starting player initialization...'); 
    document.getElementById('status').innerText = 'Initializing player...';

    player = new Spotify.Player({
        name: 'Consolify Web Player',
        getOAuthToken: cb => {
            console.log('Token requested by SDK');
            cb(token);
        },
        volume: 0.5
    });
    console.log('Player object created, adding listeners...');

    player.addListener('ready', ({ device_id }) => {
        console.log('READY: Device ID', device_id);  // <-- Add this
        document.getElementById('status').innerText = 'Player Ready! Device ID: ' + device_id;
        registerDevice(device_id);
    });

    player.addListener('initialization_error', ({ message }) => {
        console.error('Failed to initialize', message);
        document.getElementById('status').innerText = 'Initialization error: ' + message;
    });

    player.addListener('authentication_error', ({ message }) => {
        console.error('Failed to authenticate', message);
        document.getElementById('status').innerText = 'Authentication error: ' + message;
    });

    console.log('Connecting player...');  // <-- Add this
    player.connect().then(success => {
        console.log('Connect result:', success);  // <-- Add this
    });
}

async function registerDevice(deviceId) {
    try {
        await fetch('/api/device', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ deviceId: deviceId })
        });
        console.log('Device registered with server');
    } catch (error) {
        console.error('Error registering device:', error);
    }
}

function playPause() {
    if (player) {
        player.togglePlay();
    }
}