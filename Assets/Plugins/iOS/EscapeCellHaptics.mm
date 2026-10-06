// Taptic Engine feedback for Escape Cell (called from Haptics.cs through DllImport("__Internal")).
#import <UIKit/UIKit.h>

extern "C" {

// style: 0 = light, 1 = medium, 2 = heavy, 3 = rigid, 4 = soft
void EscapeCell_Impact(int style, float intensity)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIImpactFeedbackStyle s = UIImpactFeedbackStyleLight;
        switch (style) {
            case 1: s = UIImpactFeedbackStyleMedium; break;
            case 2: s = UIImpactFeedbackStyleHeavy; break;
            case 3: s = UIImpactFeedbackStyleRigid; break;
            case 4: s = UIImpactFeedbackStyleSoft; break;
            default: break;
        }
        UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
        [generator prepare];
        [generator impactOccurredWithIntensity:MAX(0.0, MIN(1.0, intensity))];
    });
}

// type: 0 = success, 1 = warning, 2 = error
void EscapeCell_Notify(int type)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UINotificationFeedbackType t = UINotificationFeedbackTypeSuccess;
        if (type == 1) t = UINotificationFeedbackTypeWarning;
        if (type == 2) t = UINotificationFeedbackTypeError;
        UINotificationFeedbackGenerator *generator = [[UINotificationFeedbackGenerator alloc] init];
        [generator prepare];
        [generator notificationOccurred:t];
    });
}

}
