# 📱 React Native Implementation: Driver Decline & Auto-Reassignment

## Overview
This guide shows how to implement the automatic ride reassignment feature in your React Native app.

## Key Concepts

### Ride Status Flow
```
Pending (waiting for driver)
   ↓
Accepted (driver accepted)
   ↓
   [Driver can now decline]
   ↓
Pending (reassignment happens) ← NEW: Ride back to Pending
   ↓
[Another driver accepts] → Accepted
```

## Implementation

### 1. Setup SignalR Connection with Auto-Subscribe

```javascript
import * as signalR from '@microsoft/signalr';
import { useEffect, useRef, useCallback, useState } from 'react';

export const useRideHub = (userId) => {
  const connectionRef = useRef(null);
  const [rideStatus, setRideStatus] = useState('Pending');
  const [isReassigning, setIsReassigning] = useState(false);

  useEffect(() => {
    if (!userId) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('http://your-backend.com:5000/userhub', {
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .build();

    connection.start()
      .then(() => {
        console.log('✅ Connected to UserHub');
        
        // Subscribe to user-specific events
        connection.invoke('SubscribeUser', userId.toString())
          .catch(err => console.error('Subscribe error:', err));
      })
      .catch(err => console.error('Connection error:', err));

    // 🎯 MAIN EVENT: Handle all ride status updates
    connection.on('RideStatusUpdated', (rideId, status, payload) => {
      console.log('🚕 Ride Status Update:', {
        rideId,
        status: payload.status,
        message: payload.message,
      });

      // Check if this is a reassignment scenario
      if (payload.status === 'Pending' && payload.message.includes('Searching')) {
        handleDriverDeclined(payload);
      } else if (payload.status === 'Accepted') {
        handleNewDriverAccepted(payload);
      } else if (payload.status === 'OnWay') {
        handleDriverOnWay(payload);
      } else if (payload.status === 'Completed') {
        handleRideCompleted(payload);
      } else if (payload.status === 'Canceled') {
        handleRideCanceled(payload);
      }
    });

    connectionRef.current = connection;

    return () => {
      if (connectionRef.current) {
        connectionRef.current.stop();
      }
    };
  }, [userId]);

  return connectionRef.current;
};
```

### 2. Handle Driver Declined Event

```javascript
const handleDriverDeclined = (payload) => {
  console.log('👋 Driver declined ride:', payload.message);
  
  // Show loading indicator
  setIsReassigning(true);
  
  // Update UI
  showNotification({
    type: 'info',
    message: payload.message, // "Driver not available. Searching for another driver..."
    duration: 3000,
  });

  // Disable any "Accept" button during reassignment
  setAcceptButtonDisabled(true);
  
  // Show loading spinner
  showLoadingSpinner('Finding another driver...');
};
```

### 3. Handle New Driver Accepted

```javascript
const handleNewDriverAccepted = (payload) => {
  console.log('✅ New driver accepted ride:', payload);
  
  // Hide loading spinner
  hideLoadingSpinner();
  setIsReassigning(false);

  // Show success notification
  showNotification({
    type: 'success',
    message: 'Driver accepted your ride!',
    duration: 2000,
  });

  // Update UI with new driver info
  updateRideScreen({
    driverId: payload.driverId,
    driverName: payload.driverName,
    driverRating: payload.driverRating,
    driverPhone: payload.driverPhone,
    vehicleInfo: payload.vehicleInfo,
  });

  // Enable "Start" button when ready
  setStartButtonDisabled(false);
  setRideStatus('Accepted');
};
```

### 4. Complete UI Component Example

```javascript
import React, { useState, useEffect } from 'react';
import { View, Text, TouchableOpacity, ActivityIndicator, Alert } from 'react-native';
import { useRideHub } from './hooks/useRideHub';

export const RideActiveScreen = ({ route, navigation }) => {
  const { rideId, userId, pickupLocation, destination } = route.params;
  
  const connection = useRideHub(userId);
  const [rideStatus, setRideStatus] = useState('Pending');
  const [isReassigning, setIsReassigning] = useState(false);
  const [driver, setDriver] = useState(null);
  const [statusMessage, setStatusMessage] = useState('Finding a driver...');

  // Listen to ride updates
  useEffect(() => {
    if (!connection) return;

    connection.on('RideStatusUpdated', (rideId, status, payload) => {
      setRideStatus(payload.status);
      setStatusMessage(payload.message);

      if (payload.status === 'Pending' && payload.message.includes('Searching')) {
        // Driver declined - show reassignment UI
        setIsReassigning(true);
      } else if (payload.status === 'Accepted') {
        // New driver accepted
        setIsReassigning(false);
        setDriver({
          name: payload.driverName,
          rating: payload.driverRating,
          location: payload.driverLocation,
        });
      }
    });
  }, [connection]);

  const handleCancelRide = async () => {
    Alert.alert(
      'Cancel Ride',
      'Are you sure you want to cancel this ride?',
      [
        { text: 'Keep Ride', onPress: () => {}, style: 'cancel' },
        {
          text: 'Cancel',
          onPress: async () => {
            try {
              const response = await fetch(
                `http://your-backend.com:5000/api/rides/${rideId}/cancel/user`,
                {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ userId: userId }),
                }
              );
              const data = await response.json();
              if (response.ok) {
                navigation.navigate('Home');
              }
            } catch (error) {
              Alert.alert('Error', 'Failed to cancel ride');
            }
          },
          style: 'destructive',
        },
      ]
    );
  };

  return (
    <View style={styles.container}>
      {/* Ride Info */}
      <View style={styles.rideInfoCard}>
        <Text style={styles.rideTitle}>Ride #{rideId}</Text>
        <Text style={styles.statusText}>Status: {rideStatus}</Text>
      </View>

      {/* Reassignment Indicator */}
      {isReassigning ? (
        <View style={styles.reassigningCard}>
          <ActivityIndicator size="large" color="#007bff" />
          <Text style={styles.reassigningText}>
            {statusMessage}
          </Text>
          <Text style={styles.reassigningSubtext}>
            Please wait while we find another driver...
          </Text>
        </View>
      ) : (
        <View style={styles.driverCard}>
          {driver ? (
            <>
              <Text style={styles.driverName}>{driver.name}</Text>
              <Text style={styles.driverRating}>⭐ {driver.rating}</Text>
              <Text style={styles.driverLocation}>
                📍 {driver.location?.address || 'Locating...'}
              </Text>
            </>
          ) : (
            <Text style={styles.waitingText}>Waiting for driver acceptance...</Text>
          )}
        </View>
      )}

      {/* Action Buttons */}
      <View style={styles.buttonContainer}>
        <TouchableOpacity
          style={[styles.button, styles.cancelButton]}
          onPress={handleCancelRide}
          disabled={isReassigning} // Disable during reassignment
        >
          <Text style={styles.buttonText}>Cancel Ride</Text>
        </TouchableOpacity>
      </View>
    </View>
  );
};

const styles = {
  container: {
    flex: 1,
    padding: 20,
    backgroundColor: '#f5f5f5',
  },
  rideInfoCard: {
    backgroundColor: 'white',
    padding: 15,
    borderRadius: 8,
    marginBottom: 15,
    shadowColor: '#000',
    shadowOpacity: 0.1,
    shadowRadius: 3,
    elevation: 3,
  },
  rideTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 5,
  },
  statusText: {
    fontSize: 14,
    color: '#666',
  },
  reassigningCard: {
    backgroundColor: '#e7f3ff',
    padding: 20,
    borderRadius: 8,
    marginBottom: 15,
    alignItems: 'center',
  },
  reassigningText: {
    marginTop: 15,
    fontSize: 16,
    fontWeight: '600',
    color: '#0056b3',
  },
  reassigningSubtext: {
    marginTop: 5,
    fontSize: 13,
    color: '#666',
    textAlign: 'center',
  },
  driverCard: {
    backgroundColor: 'white',
    padding: 15,
    borderRadius: 8,
    marginBottom: 15,
  },
  driverName: {
    fontSize: 16,
    fontWeight: 'bold',
    marginBottom: 5,
  },
  driverRating: {
    fontSize: 14,
    color: '#f39c12',
    marginBottom: 5,
  },
  driverLocation: {
    fontSize: 13,
    color: '#666',
  },
  waitingText: {
    fontSize: 14,
    color: '#999',
    fontStyle: 'italic',
  },
  buttonContainer: {
    flexDirection: 'row',
    gap: 10,
  },
  button: {
    flex: 1,
    paddingVertical: 12,
    borderRadius: 8,
    alignItems: 'center',
  },
  cancelButton: {
    backgroundColor: '#dc3545',
  },
  buttonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: 'bold',
  },
};
```

### 5. Handle Various Scenarios

```javascript
// Scenario 1: Driver Declines → System Reassigns
export const handleDriverDecline = (payload) => {
  const { rideId, message } = payload;
  
  // UI Updates
  setLoadingVisible(true);
  setLoadingText(message); // "Driver not available. Searching for another driver..."
  
  // Analytics
  logEvent('driver_declined', { rideId });
  
  // Optional: Show toast notification
  showToast(message, 'info', 3000);
};

// Scenario 2: New Driver Accepts After Decline
export const handleReassignmentSuccess = (payload) => {
  const { rideId, driverId, driverName } = payload;
  
  // UI Updates
  setLoadingVisible(false);
  showToast(`New driver ${driverName} accepted your ride!`, 'success', 2000);
  
  // Analytics
  logEvent('driver_reassigned', { rideId, newDriverId: driverId });
  
  // Refresh driver UI
  fetchDriverDetails(driverId);
};

// Scenario 3: No Drivers Available
export const handleReassignmentFailed = (payload) => {
  const { rideId, message } = payload;
  
  // UI Updates
  setLoadingVisible(false);
  Alert.alert(
    'No Drivers Available',
    message,
    [
      {
        text: 'Keep Waiting',
        onPress: () => {
          // Keep the ride active
          setLoadingVisible(true);
          setLoadingText('Waiting for available drivers...');
        },
      },
      {
        text: 'Cancel Ride',
        onPress: () => cancelRide(rideId),
        style: 'destructive',
      },
    ]
  );
};
```

## Testing in React Native

### Test Case 1: Normal Reassignment

```javascript
// Simulate driver decline
describe('RideReassignment', () => {
  it('should show loading when driver declines', async () => {
    // Setup
    const { getByText } = render(<RideActiveScreen />);
    
    // Simulate driver decline event
    connection.emit('RideStatusUpdated', 123, 'Pending', {
      status: 'Pending',
      message: 'Driver not available. Searching for another driver...'
    });
    
    // Assert
    expect(getByText(/Searching for another driver/)).toBeTruthy();
    expect(getByText('Finding another driver...')).toBeTruthy();
  });

  it('should show new driver when reassignment succeeds', async () => {
    // Setup with active reassignment
    // Simulate new driver accepted
    connection.emit('RideStatusUpdated', 123, 'Accepted', {
      status: 'Accepted',
      driverName: 'Ahmed',
      driverRating: 4.8,
      message: 'Driver accepted your ride!'
    });
    
    // Assert
    expect(getByText('Ahmed')).toBeTruthy();
    expect(getByText('⭐ 4.8')).toBeTruthy();
  });
});
```

## Key Takeaways for React Native

1. **Always subscribe** to the UserHub before creating a ride
2. **Listen for RideStatusUpdated** events—this handles all scenarios
3. **Show loading during reassignment**—keep the user informed
4. **Disable actions during reassignment**—prevent conflicting requests
5. **Handle no-driver scenario gracefully**—offer clear options to user
6. **Update UI reactively**—when new driver accepts, immediately show their info

## Common Issues & Solutions

### Issue: User doesn't see "Searching for another driver" message

**Solution:**
```javascript
// Ensure connection is established BEFORE creating ride
await connection.start();
await connection.invoke('SubscribeUser', userId.toString());

// Then create ride
await createRide(rideRequest);
```

### Issue: Multiple drivers decline and reassignment keeps happening

**Solution:**
- Backend logs each reassignment
- Monitor `CurrentRideId` in driver Redis hash
- Ensure driver exclusion logic works: `if (did === request.DriverId) continue;`

### Issue: "No drivers available" but drivers exist

**Solution:**
- Check driver status in Redis: should be `"Free"`
- Verify `updatedAt` is recent (within 60 seconds)
- Check geo-spatial index has drivers: `GEORADIUS geo:drivers:eco ...`

## Payload Structure Reference

```typescript
interface RideStatusUpdatePayload {
  rideId: number;
  status: 'Pending' | 'Accepted' | 'OnWay' | 'Completed' | 'Canceled';
  message: string;
  finalPrice?: number;
  clearMap?: boolean;
  driverId?: number;
  driverName?: string;
  driverRating?: number;
  driverPhone?: string;
  driverLocation?: {
    lat: number;
    lng: number;
    address?: string;
  };
  vehicleType?: string;
  vehicleNumber?: string;
}
```

## Next Steps

1. Implement the hook in your project
2. Test with multiple drivers
3. Add animations when status changes
4. Implement sound notifications for status updates
5. Add analytics to track reassignment metrics
