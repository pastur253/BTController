# Keep reflection methods for BluetoothProfile
-keepclassmembers class android.bluetooth.BluetoothA2dp {
    public boolean connect(android.bluetooth.BluetoothDevice);
    public boolean disconnect(android.bluetooth.BluetoothDevice);
}
-keepclassmembers class android.bluetooth.BluetoothHeadset {
    public boolean connect(android.bluetooth.BluetoothDevice);
    public boolean disconnect(android.bluetooth.BluetoothDevice);
}
-keepclassmembers class android.bluetooth.BluetoothProfile {
    public *;
}
