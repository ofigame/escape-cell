// Clears the phone's local notifications for Escape Cell (called from OldNotes.cs through DllImport("__Internal")).
// The removed open world scheduled farm and raid notes with the system; they stay scheduled through an update, and
// the game no longer carries the notifications package that could take them back. UserNotifications is loaded by
// hand so the app needs no extra framework linked.
#import <UIKit/UIKit.h>
#import <objc/message.h>
#include <dlfcn.h>

extern "C" {

void EscapeCell_ClearNotes()
{
    dlopen("/System/Library/Frameworks/UserNotifications.framework/UserNotifications", RTLD_LAZY);
    Class centerClass = NSClassFromString(@"UNUserNotificationCenter");
    if (centerClass != nil) {
        id center = ((id (*)(id, SEL))objc_msgSend)(centerClass, NSSelectorFromString(@"currentNotificationCenter"));
        if (center != nil) {
            ((void (*)(id, SEL))objc_msgSend)(center, NSSelectorFromString(@"removeAllPendingNotificationRequests"));
            ((void (*)(id, SEL))objc_msgSend)(center, NSSelectorFromString(@"removeAllDeliveredNotifications"));
        }
    }
    dispatch_async(dispatch_get_main_queue(), ^{
        [UIApplication sharedApplication].applicationIconBadgeNumber = 0;
    });
}

}
