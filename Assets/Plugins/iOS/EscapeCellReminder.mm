// The daily reminder for foi (called from Reminder.cs through DllImport("__Internal")): asks the player once, then one
// local notification for the next evening, replaced each time the game opens (so it only comes on days away from it).
// UserNotifications is loaded by hand, as in EscapeCellNotes.mm, so the app needs no extra framework linked.
#import <UIKit/UIKit.h>
#import <objc/message.h>
#include <dlfcn.h>

extern "C" {

static id ReminderCenter()
{
    dlopen("/System/Library/Frameworks/UserNotifications.framework/UserNotifications", RTLD_LAZY);
    Class centerClass = NSClassFromString(@"UNUserNotificationCenter");
    if (centerClass == nil) return nil;
    return ((id (*)(id, SEL))objc_msgSend)(centerClass, NSSelectorFromString(@"currentNotificationCenter"));
}

// One of a few notes (a slot each); seconds under a minute: the slot's note is taken back.
void EscapeCell_ScheduleNote(int slot, const char* title, const char* body, double seconds)
{
    id center = ReminderCenter();
    if (center == nil) return;
    NSString* ident = [NSString stringWithFormat:@"foi.note.%d", slot];
    if (seconds < 60)
    {
        ((void (*)(id, SEL, id))objc_msgSend)(center, NSSelectorFromString(@"removePendingNotificationRequestsWithIdentifiers:"), @[ident]);
        return;
    }
    NSString* t = [NSString stringWithUTF8String:title ? title : ""];
    NSString* b = [NSString stringWithUTF8String:body ? body : ""];
    // alert | sound | badge
    NSUInteger options = (1 << 2) | (1 << 1) | (1 << 0);
    void (^asked)(BOOL, NSError*) = ^(BOOL granted, NSError* error) {
        if (!granted) return;
        Class contentClass = NSClassFromString(@"UNMutableNotificationContent");
        Class triggerClass = NSClassFromString(@"UNTimeIntervalNotificationTrigger");
        Class requestClass = NSClassFromString(@"UNNotificationRequest");
        Class soundClass = NSClassFromString(@"UNNotificationSound");
        if (contentClass == nil || triggerClass == nil || requestClass == nil) return;
        id content = [[contentClass alloc] init];
        ((void (*)(id, SEL, id))objc_msgSend)(content, NSSelectorFromString(@"setTitle:"), t);
        ((void (*)(id, SEL, id))objc_msgSend)(content, NSSelectorFromString(@"setBody:"), b);
        if (soundClass != nil)
        {
            id sound = ((id (*)(id, SEL))objc_msgSend)(soundClass, NSSelectorFromString(@"defaultSound"));
            ((void (*)(id, SEL, id))objc_msgSend)(content, NSSelectorFromString(@"setSound:"), sound);
        }
        id trigger = ((id (*)(id, SEL, double, BOOL))objc_msgSend)(triggerClass, NSSelectorFromString(@"triggerWithTimeInterval:repeats:"), seconds, NO);
        id request = ((id (*)(id, SEL, id, id, id))objc_msgSend)(requestClass, NSSelectorFromString(@"requestWithIdentifier:content:trigger:"), ident, content, trigger);
        ((void (*)(id, SEL, id, id))objc_msgSend)(center, NSSelectorFromString(@"addNotificationRequest:withCompletionHandler:"), request, nil);
    };
    ((void (*)(id, SEL, NSUInteger, id))objc_msgSend)(center, NSSelectorFromString(@"requestAuthorizationWithOptions:completionHandler:"), options, asked);
}

}
