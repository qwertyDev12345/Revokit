#import "NotificationPromptViewController.h"

@interface NotificationPromptViewController ()
@property (nonatomic, strong) UIImageView *bgImageView;
@property (nonatomic, strong) UIView *contentView;
@property (nonatomic, strong) UILabel *titleLabel;
@property (nonatomic, strong) UILabel *messageLabel;
@property (nonatomic, strong) UIButton *allowButton;
@property (nonatomic, strong) UIButton *cancelButton;
@property (nonatomic, copy) NotificationPromptHandler allowHandler;
@property (nonatomic, copy) NotificationPromptHandler cancelHandler;
/// Kept so we can resize it on rotation (it lives inside gradView, not self.view.layer directly).
@property (nonatomic, strong) CAGradientLayer *gradientLayer;
@property (nonatomic, assign) BOOL handledAction;
@end

@implementation NotificationPromptViewController

- (instancetype)initWithTitle:(NSString *)title
                      message:(NSString *)message
              backgroundImage:(UIImage *)image
                 allowHandler:(NotificationPromptHandler)allowHandler
                  cancelHandler:(NotificationPromptHandler)cancelHandler
{
    self = [super initWithNibName:nil bundle:nil];
    if (!self) return nil;

    _allowHandler = [allowHandler copy];
    _cancelHandler = [cancelHandler copy];

    self.modalPresentationStyle = UIModalPresentationFullScreen;
    self.view.backgroundColor = [UIColor blackColor];

    // Background image — использует тот же задник что и экран загрузки
    _bgImageView = [[UIImageView alloc] initWithImage:[UIImage imageNamed:@"LaunchBackground"]];
    _bgImageView.contentMode = UIViewContentModeScaleAspectFill;
    _bgImageView.translatesAutoresizingMaskIntoConstraints = NO;
    _bgImageView.clipsToBounds = YES;
    [self.view addSubview:_bgImageView];

    // Gradient overlay to darken image
    _gradientLayer = [CAGradientLayer layer];
    _gradientLayer.colors = @[(id)[UIColor colorWithWhite:0.0 alpha:0.45].CGColor,
                               (id)[UIColor colorWithWhite:0.0 alpha:0.65].CGColor];
    _gradientLayer.startPoint = CGPointMake(0.5, 0.0);
    _gradientLayer.endPoint = CGPointMake(0.5, 1.0);

    UIView *gradView = [UIView new];
    gradView.translatesAutoresizingMaskIntoConstraints = NO;
    [self.view addSubview:gradView];
    // frame will be set to final bounds in viewDidLayoutSubviews
    [gradView.layer insertSublayer:_gradientLayer atIndex:0];

    // The viewport stays inside the safe area. Tall content can scroll instead
    // of compressing labels or pushing the actions off a landscape screen.
    UIScrollView *scrollView = [UIScrollView new];
    scrollView.translatesAutoresizingMaskIntoConstraints = NO;
    scrollView.contentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentNever;
    scrollView.alwaysBounceVertical = NO;
    [self.view addSubview:scrollView];

    UIView *scrollContent = [UIView new];
    scrollContent.translatesAutoresizingMaskIntoConstraints = NO;
    [scrollView addSubview:scrollContent];

    // Container for labels/buttons
    _contentView = [UIView new];
    _contentView.translatesAutoresizingMaskIntoConstraints = NO;
    _contentView.backgroundColor = [UIColor clearColor];
    [scrollContent addSubview:_contentView];

    _titleLabel = [UILabel new];
    _titleLabel.translatesAutoresizingMaskIntoConstraints = NO;
    _titleLabel.text = title ?: @"";
    _titleLabel.textColor = [UIColor whiteColor];
    _titleLabel.font = [UIFont systemFontOfSize:28 weight:UIFontWeightBold];
    _titleLabel.textAlignment = NSTextAlignmentCenter;
    _titleLabel.numberOfLines = 0;
    _titleLabel.lineBreakMode = NSLineBreakByWordWrapping;
    [_titleLabel setContentCompressionResistancePriority:UILayoutPriorityRequired
                                                forAxis:UILayoutConstraintAxisVertical];
    [self.contentView addSubview:_titleLabel];

    _messageLabel = [UILabel new];
    _messageLabel.translatesAutoresizingMaskIntoConstraints = NO;
    _messageLabel.text = message ?: @"";
    _messageLabel.textColor = [UIColor colorWithWhite:1.0 alpha:0.9];
    _messageLabel.font = [UIFont systemFontOfSize:15 weight:UIFontWeightRegular];
    _messageLabel.textAlignment = NSTextAlignmentCenter;
    _messageLabel.numberOfLines = 0;
    _messageLabel.lineBreakMode = NSLineBreakByWordWrapping;
    [_messageLabel setContentCompressionResistancePriority:UILayoutPriorityRequired
                                                  forAxis:UILayoutConstraintAxisVertical];
    [self.contentView addSubview:_messageLabel];

    // Кнопка «Allow» — яркая, акцентная
    _allowButton = [UIButton buttonWithType:UIButtonTypeCustom];
    _allowButton.translatesAutoresizingMaskIntoConstraints = NO;
    [_allowButton setTitle:@"Allow" forState:UIControlStateNormal];
    _allowButton.backgroundColor = [UIColor colorWithRed:0.18 green:0.55 blue:1.00 alpha:1.0];
    [_allowButton setTitleColor:[UIColor whiteColor] forState:UIControlStateNormal];
    [_allowButton setTitleColor:[UIColor colorWithWhite:1.0 alpha:0.6] forState:UIControlStateHighlighted];
    _allowButton.titleLabel.font = [UIFont systemFontOfSize:17 weight:UIFontWeightBold];
    _allowButton.layer.cornerRadius = 14;
    _allowButton.layer.shadowColor = [UIColor colorWithRed:0.18 green:0.55 blue:1.00 alpha:1.0].CGColor;
    _allowButton.layer.shadowOffset = CGSizeMake(0, 4);
    _allowButton.layer.shadowOpacity = 0.55;
    _allowButton.layer.shadowRadius = 10;
    [_allowButton addTarget:self action:@selector(onAllow:) forControlEvents:UIControlEventTouchUpInside];
    [self.contentView addSubview:_allowButton];

    // Кнопка «Not Now» — тусклая, без фона, просто текст
    _cancelButton = [UIButton buttonWithType:UIButtonTypeCustom];
    _cancelButton.translatesAutoresizingMaskIntoConstraints = NO;
    [_cancelButton setTitle:@"Not Now" forState:UIControlStateNormal];
    _cancelButton.backgroundColor = [UIColor clearColor];
    [_cancelButton setTitleColor:[UIColor colorWithWhite:1.0 alpha:0.40] forState:UIControlStateNormal];
    [_cancelButton setTitleColor:[UIColor colorWithWhite:1.0 alpha:0.20] forState:UIControlStateHighlighted];
    _cancelButton.titleLabel.font = [UIFont systemFontOfSize:14 weight:UIFontWeightRegular];
    [_cancelButton addTarget:self action:@selector(onCancel:) forControlEvents:UIControlEventTouchUpInside];
    [self.contentView addSubview:_cancelButton];

    // Layout
    [NSLayoutConstraint activateConstraints:@[
        [self.bgImageView.topAnchor constraintEqualToAnchor:self.view.topAnchor],
        [self.bgImageView.bottomAnchor constraintEqualToAnchor:self.view.bottomAnchor],
        [self.bgImageView.leadingAnchor constraintEqualToAnchor:self.view.leadingAnchor],
        [self.bgImageView.trailingAnchor constraintEqualToAnchor:self.view.trailingAnchor],

        [gradView.topAnchor constraintEqualToAnchor:self.view.topAnchor],
        [gradView.bottomAnchor constraintEqualToAnchor:self.view.bottomAnchor],
        [gradView.leadingAnchor constraintEqualToAnchor:self.view.leadingAnchor],
        [gradView.trailingAnchor constraintEqualToAnchor:self.view.trailingAnchor],

        [scrollView.topAnchor constraintEqualToAnchor:self.view.safeAreaLayoutGuide.topAnchor],
        [scrollView.bottomAnchor constraintEqualToAnchor:self.view.safeAreaLayoutGuide.bottomAnchor],
        [scrollView.leadingAnchor constraintEqualToAnchor:self.view.safeAreaLayoutGuide.leadingAnchor constant:16],
        [scrollView.trailingAnchor constraintEqualToAnchor:self.view.safeAreaLayoutGuide.trailingAnchor constant:-16],
        [scrollContent.topAnchor constraintEqualToAnchor:scrollView.contentLayoutGuide.topAnchor],
        [scrollContent.bottomAnchor constraintEqualToAnchor:scrollView.contentLayoutGuide.bottomAnchor],
        [scrollContent.leadingAnchor constraintEqualToAnchor:scrollView.contentLayoutGuide.leadingAnchor],
        [scrollContent.trailingAnchor constraintEqualToAnchor:scrollView.contentLayoutGuide.trailingAnchor],
        [scrollContent.widthAnchor constraintEqualToAnchor:scrollView.frameLayoutGuide.widthAnchor],
        [scrollContent.heightAnchor constraintGreaterThanOrEqualToAnchor:scrollView.frameLayoutGuide.heightAnchor],
        // Fill the viewport when possible; grow vertically when the text needs it.
        ({ NSLayoutConstraint *c = [scrollContent.heightAnchor
                constraintEqualToAnchor:scrollView.frameLayoutGuide.heightAnchor];
            c.priority = UILayoutPriorityDefaultLow; c; }),
        [self.contentView.centerXAnchor constraintEqualToAnchor:scrollContent.centerXAnchor],
        [self.contentView.centerYAnchor constraintEqualToAnchor:scrollContent.centerYAnchor],
        [self.contentView.topAnchor
            constraintGreaterThanOrEqualToAnchor:scrollContent.topAnchor constant:16],
        [self.contentView.bottomAnchor
            constraintLessThanOrEqualToAnchor:scrollContent.bottomAnchor constant:-16],
        // Keep comfortable margins and cap the text width on wide displays.
        ({  NSLayoutConstraint *c = [self.contentView.widthAnchor
                constraintEqualToAnchor:self.view.widthAnchor multiplier:0.82];
            c.priority = UILayoutPriorityDefaultHigh; c; }),
        [self.contentView.widthAnchor constraintLessThanOrEqualToAnchor:scrollContent.widthAnchor],
        [self.contentView.widthAnchor constraintLessThanOrEqualToConstant:480],

        [self.titleLabel.topAnchor constraintEqualToAnchor:self.contentView.topAnchor],
        [self.titleLabel.leadingAnchor constraintEqualToAnchor:self.contentView.leadingAnchor],
        [self.titleLabel.trailingAnchor constraintEqualToAnchor:self.contentView.trailingAnchor],

        [self.messageLabel.topAnchor constraintEqualToAnchor:self.titleLabel.bottomAnchor constant:12],
        [self.messageLabel.leadingAnchor constraintEqualToAnchor:self.contentView.leadingAnchor],
        [self.messageLabel.trailingAnchor constraintEqualToAnchor:self.contentView.trailingAnchor],

        [self.allowButton.topAnchor constraintEqualToAnchor:self.messageLabel.bottomAnchor constant:22],
        [self.allowButton.leadingAnchor constraintEqualToAnchor:self.contentView.leadingAnchor],
        [self.allowButton.trailingAnchor constraintEqualToAnchor:self.contentView.trailingAnchor],
        [self.allowButton.heightAnchor constraintEqualToConstant:48],

        [self.cancelButton.topAnchor constraintEqualToAnchor:self.allowButton.bottomAnchor constant:12],
        [self.cancelButton.leadingAnchor constraintEqualToAnchor:self.contentView.leadingAnchor],
        [self.cancelButton.trailingAnchor constraintEqualToAnchor:self.contentView.trailingAnchor],
        [self.cancelButton.heightAnchor constraintEqualToConstant:44],

        [self.cancelButton.bottomAnchor constraintEqualToAnchor:self.contentView.bottomAnchor],
    ]];

    return self;
}

- (void)viewDidLayoutSubviews
{
    [super viewDidLayoutSubviews];
    // Keep gradient filling the whole screen on every rotation.
    _gradientLayer.frame = self.view.bounds;
}

- (void)onAllow:(id)sender
{
    if (self.handledAction) return;
    self.handledAction = YES;
    self.allowButton.enabled = self.cancelButton.enabled = NO;
    [self dismissViewControllerAnimated:YES completion:^{
        if (self.allowHandler) self.allowHandler();
    }];
}

- (void)onCancel:(id)sender
{
    if (self.handledAction) return;
    self.handledAction = YES;
    self.allowButton.enabled = self.cancelButton.enabled = NO;
    [self dismissViewControllerAnimated:YES completion:^{
        if (self.cancelHandler) self.cancelHandler();
    }];
}

@end
