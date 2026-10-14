package com.ofigame.foi;

import android.app.Activity;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.os.Build;
import android.util.Log;

import com.google.firebase.FirebaseApp;
import com.google.firebase.FirebaseOptions;
import com.google.firebase.messaging.FirebaseMessaging;
import com.unity3d.player.UnityPlayer;

/**
 * The challenge's phone notifications: starts Firebase with the game's own project settings (no google-services
 * file needed), makes the "challenge" channel and hands the phone's token back to the game (UnitySendMessage).
 * Notifications sent while the game is closed are shown by the system itself.
 */
public class FoiPush {
    public static void init(final Activity activity, String appId, String apiKey, String projectId, String senderId,
                            final String receiver, final String method) {
        try {
            if (Build.VERSION.SDK_INT >= 26) {
                NotificationChannel channel = new NotificationChannel("challenge", "Challenge", NotificationManager.IMPORTANCE_HIGH);
                NotificationManager manager = activity.getSystemService(NotificationManager.class);
                if (manager != null) manager.createNotificationChannel(channel);
            }
            if (FirebaseApp.getApps(activity).isEmpty()) {
                FirebaseOptions options = new FirebaseOptions.Builder()
                        .setApplicationId(appId).setApiKey(apiKey).setProjectId(projectId).setGcmSenderId(senderId).build();
                FirebaseApp.initializeApp(activity.getApplicationContext(), options);
            }
            FirebaseMessaging.getInstance().getToken().addOnCompleteListener(task -> {
                if (task.isSuccessful() && task.getResult() != null) UnityPlayer.UnitySendMessage(receiver, method, task.getResult());
                else Log.w("FoiPush", "no token", task.getException());
            });
        } catch (Throwable t) {
            Log.w("FoiPush", "push off", t);
        }
    }
}
