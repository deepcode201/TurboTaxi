// TurboTaxi API Configuration - Auto-detect
(function() {
    // Auto-detect based on current page URL
    const currentOrigin = window.location.origin;
    const currentPort = window.location.port;
    
    // Determine API base URL
    if (currentPort === '7443' || currentPort === '7442') {
        // Kestrel - Port 7443
        window.API_BASE = 'https://localhost:7443';
        window.ENVIRONMENT = 'Development';
    } else if (currentPort === '44327' || currentPort === '61149') {
        // IIS Express or Kestrel - Port 44327
        window.API_BASE = 'https://localhost:44327';
        window.ENVIRONMENT = 'Production';
    } else if (currentOrigin.includes('anytempurl.com') || currentOrigin.includes('site4now')) {
        // Production server
        window.API_BASE = currentOrigin;
        window.ENVIRONMENT = 'Production';
    } else {
        // Fallback: use current origin
        window.API_BASE = currentOrigin;
        window.ENVIRONMENT = 'Unknown';
    }
    
    console.log('?? TurboTaxi Configuration Loaded');
    console.log('?? Current URL:', window.location.href);
    console.log('??  API Server:', window.API_BASE);
    console.log('?? SignalR Hub:', window.API_BASE + '/hubs/driver');
    console.log('???  Environment:', window.ENVIRONMENT);
    console.log('');
})();
