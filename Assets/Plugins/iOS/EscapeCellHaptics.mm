// Taptic Engine feedback for Escape Cell (called from Haptics.cs through DllImport("__Internal")).
// The generators are kept and re-prepared after each tap: a generator made and fired in the same instant
// often has its Taptic Engine still asleep, and the tap is lost or very faint.
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *impacts[5];
static UINotificationFeedbackGenerator *notifier;

static UIImpactFeedbackGenerator *ImpactFor(int style)
{
    if (style < 0 || style > 4) style = 0;
    if (impacts[style] == nil) {
        UIImpactFeedbackStyle s = UIImpactFeedbackStyleLight;
        switch (style) {
            case 1: s = UIImpactFeedbackStyleMedium; break;
            case 2: s = UIImpactFeedbackStyleHeavy; break;
            case 3: s = UIImpactFeedbackStyleRigid; break;
            case 4: s = UIImpactFeedbackStyleSoft; break;
            default: break;
        }
        impacts[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
        [impacts[style] prepare];
    }
    return impacts[style];
}

extern "C" {

// style: 0 = light, 1 = medium, 2 = heavy, 3 = rigid, 4 = soft
void EscapeCell_Impact(int style, float intensity)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIImpactFeedbackGenerator *generator = ImpactFor(style);
        [generator impactOccurredWithIntensity:MAX(0.0, MIN(1.0, intensity))];
        [generator prepare];
    });
}

// type: 0 = success, 1 = warning, 2 = error
void EscapeCell_Notify(int type)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (notifier == nil) notifier = [[UINotificationFeedbackGenerator alloc] init];
        UINotificationFeedbackType t = UINotificationFeedbackTypeSuccess;
        if (type == 1) t = UINotificationFeedbackTypeWarning;
        if (type == 2) t = UINotificationFeedbackTypeError;
        [notifier notificationOccurred:t];
        [notifier prepare];
    });
}

}
