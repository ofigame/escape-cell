package com.ofigame.foi;

import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.os.Build;
import android.util.Log;

/**
 * The daily reminder: one alarm for the next evening, replaced each time the game opens (so it only comes on days away
 * from the game); when it goes off, a notification that opens the game. Works without Google services (Huawei too).
 */
public class FoiReminder extends BroadcastReceiver {
    private static final String Channel = "daily";

    public static void schedule(Context context, String title, String body, long atMillis) {
        scheduleSlot(context, 0, title, body, atMillis);
    }

    /** One of a few notes (a slot each: the reminder, energy full, the weekend cup, the season's end); 0 or less cancels it. */
    public static void scheduleSlot(Context context, int slot, String title, String body, long atMillis) {
        try {
            Intent intent = new Intent(context, FoiReminder.class);
            intent.putExtra("title", title);
            intent.putExtra("body", body);
            intent.putExtra("slot", slot);
            PendingIntent pending = PendingIntent.getBroadcast(context, 4711 + slot * 10, intent,
                    PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
            AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
            if (alarms == null) return;
            alarms.cancel(pending);
            if (atMillis > 0) alarms.set(AlarmManager.RTC_WAKEUP, atMillis, pending); // (inexact: no special permission needed)
        } catch (Throwable t) {
            Log.w("FoiReminder", "not scheduled", t);
        }
    }

    @Override
    public void onReceive(Context context, Intent intent) {
        try {
            NotificationManager manager = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
            if (manager == null) return;
            if (Build.VERSION.SDK_INT >= 26)
                manager.createNotificationChannel(new NotificationChannel(Channel, "Daily", NotificationManager.IMPORTANCE_DEFAULT));
            Intent open = context.getPackageManager().getLaunchIntentForPackage(context.getPackageName());
            PendingIntent tap = open != null ? PendingIntent.getActivity(context, 4712, open, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE) : null;
            int icon = context.getResources().getIdentifier("app_icon", "mipmap", context.getPackageName());
            if (icon == 0) icon = context.getApplicationInfo().icon;
            Notification.Builder b = Build.VERSION.SDK_INT >= 26 ? new Notification.Builder(context, Channel) : new Notification.Builder(context);
            b.setSmallIcon(icon).setContentTitle(intent.getStringExtra("title")).setContentText(intent.getStringExtra("body")).setAutoCancel(true);
            if (tap != null) b.setContentIntent(tap);
            manager.notify(4713 + intent.getIntExtra("slot", 0) * 10, b.build());
        } catch (Throwable t) {
            Log.w("FoiReminder", "not shown", t);
        }
    }
}
