#import <UIKit/UIKit.h>
#import <AVFoundation/AVFoundation.h>
extern "C" UIViewController* UnityGetGLViewController(void);
extern "C" void UnitySendMessage(const char* object, const char* method, const char* message);

static AVSpeechSynthesizer* conesSpeech;
extern "C" void ConesSpeak(const char* text)
{
    if (!text) return;
    NSString* words = [NSString stringWithUTF8String:text];
    dispatch_async(dispatch_get_main_queue(), ^{
        if (!conesSpeech) conesSpeech = [[AVSpeechSynthesizer alloc] init];
        [conesSpeech stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
        AVSpeechUtterance* utterance = [AVSpeechUtterance speechUtteranceWithString:words];
        utterance.voice = [AVSpeechSynthesisVoice voiceWithLanguage:@"en-US"];
        utterance.rate = AVSpeechUtteranceDefaultSpeechRate;
        [conesSpeech speakUtterance:utterance];
    });
}
extern "C" void ConesStopSpeaking()
{
    dispatch_async(dispatch_get_main_queue(), ^{ [conesSpeech stopSpeakingAtBoundary:AVSpeechBoundaryImmediate]; });
}

@interface ConesDocumentDelegate : NSObject <UIDocumentPickerDelegate>
@end
@implementation ConesDocumentDelegate
- (void)documentPicker:(UIDocumentPickerViewController*)controller didPickDocumentsAtURLs:(NSArray<NSURL*>*)urls
{
    NSURL* url = urls.firstObject;
    BOOL scoped = [url startAccessingSecurityScopedResource];
    NSNumber* size = nil;
    [url getResourceValue:&size forKey:NSURLFileSizeKey error:nil];
    NSData* data = nil;
    if (url && size && size.unsignedLongLongValue <= 262144)
        data = [NSData dataWithContentsOfURL:url options:NSDataReadingMappedIfSafe error:nil];
    if (scoped) [url stopAccessingSecurityScopedResource];
    NSString* json = data.length > 0 && data.length <= 262144 ? [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] : nil;
    if (json) UnitySendMessage("Cones App", "ReceiveDrillFile", json.UTF8String);
    else UnitySendMessage("Cones App", "ReceiveDrillError", "Unreadable file");
#if !__has_feature(objc_arc)
    [json release];
#endif
}
@end

static ConesDocumentDelegate* conesDocumentDelegate;
extern "C" void ConesPickDrill()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* root = UnityGetGLViewController();
        if (!root || root.presentedViewController) return;
        if (!conesDocumentDelegate) conesDocumentDelegate = [[ConesDocumentDelegate alloc] init];
        UIDocumentPickerViewController* picker = [[UIDocumentPickerViewController alloc] initWithDocumentTypes:@[@"public.item"] inMode:UIDocumentPickerModeImport];
        picker.delegate = conesDocumentDelegate;
        picker.allowsMultipleSelection = NO;
        [root presentViewController:picker animated:YES completion:nil];
#if !__has_feature(objc_arc)
        [picker release];
#endif
    });
}

extern "C" void ConesShareFile(const char* path)
{
    if (!path) return;
    NSString* filePath = [NSString stringWithUTF8String:path];
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* root = UnityGetGLViewController();
        if (!root || root.presentedViewController || ![[NSFileManager defaultManager] fileExistsAtPath:filePath]) return;
        UIActivityViewController* activity = [[UIActivityViewController alloc] initWithActivityItems:@[[NSURL fileURLWithPath:filePath]] applicationActivities:nil];
        UIPopoverPresentationController* popover = activity.popoverPresentationController;
        if (popover) { popover.sourceView = root.view; popover.sourceRect = CGRectMake(CGRectGetMidX(root.view.bounds), CGRectGetMidY(root.view.bounds), 1, 1); popover.permittedArrowDirections = 0; }
        [root presentViewController:activity animated:YES completion:nil];
#if !__has_feature(objc_arc)
        [activity release];
#endif
    });
}

extern "C" void ConesShareImage(const char* path)
{
    if (!path) return;
    NSString* filePath = [NSString stringWithUTF8String:path];
    dispatch_async(dispatch_get_main_queue(), ^{
        UIImage* image = [UIImage imageWithContentsOfFile:filePath];
        UIViewController* root = UnityGetGLViewController();
        if (!image || !root) return;
        while (root.presentedViewController) {
            if ([root.presentedViewController isKindOfClass:[UIActivityViewController class]]) return;
            root = root.presentedViewController;
        }
        UIActivityViewController* activity = [[UIActivityViewController alloc] initWithActivityItems:@[image] applicationActivities:nil];
        UIPopoverPresentationController* popover = activity.popoverPresentationController;
        if (popover) {
            popover.sourceView = root.view;
            popover.sourceRect = CGRectMake(CGRectGetMidX(root.view.bounds), CGRectGetMidY(root.view.bounds), 1, 1);
            popover.permittedArrowDirections = 0;
        }
        [root presentViewController:activity animated:YES completion:nil];
#if !__has_feature(objc_arc)
        [activity release];
#endif
    });
}
