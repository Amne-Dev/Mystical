pragma Singleton
import QtQuick 2.15
import QtQuick.Animation 1.15

QtObject {
    id: animations
    
    // Standard animation components that can be reused throughout the app
    
    // Fade animations
    function createFadeIn(target, duration) {
        return fadeInComponent.createObject(target, {
            "target": target,
            "duration": duration || 250
        })
    }
    
    function createFadeOut(target, duration) {
        return fadeOutComponent.createObject(target, {
            "target": target,
            "duration": duration || 250
        })
    }
    
    // Scale animations
    function createScaleIn(target, duration) {
        return scaleInComponent.createObject(target, {
            "target": target,
            "duration": duration || 300
        })
    }
    
    function createScaleOut(target, duration) {
        return scaleOutComponent.createObject(target, {
            "target": target,
            "duration": duration || 250
        })
    }
    
    // Slide animations
    function createSlideFromLeft(target, distance, duration) {
        return slideFromLeftComponent.createObject(target, {
            "target": target,
            "distance": distance || target.width,
            "duration": duration || 300
        })
    }
    
    function createSlideFromRight(target, distance, duration) {
        return slideFromRightComponent.createObject(target, {
            "target": target,
            "distance": distance || target.width,
            "duration": duration || 300
        })
    }
    
    function createSlideFromTop(target, distance, duration) {
        return slideFromTopComponent.createObject(target, {
            "target": target,
            "distance": distance || target.height,
            "duration": duration || 300
        })
    }
    
    function createSlideFromBottom(target, distance, duration) {
        return slideFromBottomComponent.createObject(target, {
            "target": target,
            "distance": distance || target.height,
            "duration": duration || 300
        })
    }
    
    // Morphing/Transform animations
    function createMorphIn(target, duration) {
        return morphInComponent.createObject(target, {
            "target": target,
            "duration": duration || 400
        })
    }
    
    // Bounce animation
    function createBounceIn(target, duration) {
        return bounceInComponent.createObject(target, {
            "target": target,
            "duration": duration || 600
        })
    }
    
    // Component definitions
    property Component fadeInComponent: Component {
        NumberAnimation {
            property: "opacity"
            from: 0
            to: 1
            easing.type: Easing.OutQuart
        }
    }
    
    property Component fadeOutComponent: Component {
        NumberAnimation {
            property: "opacity"
            from: 1
            to: 0
            easing.type: Easing.InQuart
        }
    }
    
    property Component scaleInComponent: Component {
        ParallelAnimation {
            NumberAnimation {
                property: "scale"
                from: 0.8
                to: 1.0
                easing.type: Easing.OutBack
                easing.overshoot: 1.2
            }
            NumberAnimation {
                property: "opacity"
                from: 0
                to: 1
                easing.type: Easing.OutQuart
            }
        }
    }
    
    property Component scaleOutComponent: Component {
        ParallelAnimation {
            NumberAnimation {
                property: "scale"
                from: 1.0
                to: 0.8
                easing.type: Easing.InQuart
            }
            NumberAnimation {
                property: "opacity"
                from: 1
                to: 0
                easing.type: Easing.InQuart
            }
        }
    }
    
    property Component slideFromLeftComponent: Component {
        ParallelAnimation {
            property real distance: 200
            
            NumberAnimation {
                property: "x"
                from: -distance
                to: 0
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "opacity"
                from: 0
                to: 1
                easing.type: Easing.OutQuart
            }
        }
    }
    
    property Component slideFromRightComponent: Component {
        ParallelAnimation {
            property real distance: 200
            
            NumberAnimation {
                property: "x"
                from: distance
                to: 0
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "opacity"
                from: 0
                to: 1
                easing.type: Easing.OutQuart
            }
        }
    }
    
    property Component slideFromTopComponent: Component {
        ParallelAnimation {
            property real distance: 200
            
            NumberAnimation {
                property: "y"
                from: -distance
                to: 0
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "opacity"
                from: 0
                to: 1
                easing.type: Easing.OutQuart
            }
        }
    }
    
    property Component slideFromBottomComponent: Component {
        ParallelAnimation {
            property real distance: 200
            
            NumberAnimation {
                property: "y"
                from: distance
                to: 0
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "opacity"
                from: 0
                to: 1
                easing.type: Easing.OutQuart
            }
        }
    }
    
    property Component morphInComponent: Component {
        SequentialAnimation {
            ParallelAnimation {
                NumberAnimation {
                    property: "scale"
                    from: 0.5
                    to: 1.1
                    duration: 200
                    easing.type: Easing.OutQuart
                }
                NumberAnimation {
                    property: "opacity"
                    from: 0
                    to: 1
                    duration: 200
                    easing.type: Easing.OutQuart
                }
            }
            NumberAnimation {
                property: "scale"
                from: 1.1
                to: 1.0
                duration: 200
                easing.type: Easing.OutQuart
            }
        }
    }
    
    property Component bounceInComponent: Component {
        SequentialAnimation {
            NumberAnimation {
                property: "opacity"
                from: 0
                to: 1
                duration: 100
            }
            NumberAnimation {
                property: "scale"
                from: 0.3
                to: 1.1
                duration: 200
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "scale"
                from: 1.1
                to: 0.9
                duration: 150
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "scale"
                from: 0.9
                to: 1.03
                duration: 100
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "scale"
                from: 1.03
                to: 1.0
                duration: 150
                easing.type: Easing.OutQuart
            }
        }
    }
    
    // Predefined transition components for common use cases
    property Component pageTransition: Component {
        ParallelAnimation {
            NumberAnimation {
                property: "x"
                from: 50
                to: 0
                duration: 300
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "opacity"
                from: 0
                to: 1
                duration: 300
                easing.type: Easing.OutQuart
            }
        }
    }
    
    property Component modalTransition: Component {
        ParallelAnimation {
            NumberAnimation {
                property: "scale"
                from: 0.9
                to: 1.0
                duration: 250
                easing.type: Easing.OutBack
                easing.overshoot: 1.1
            }
            NumberAnimation {
                property: "opacity"
                from: 0
                to: 1
                duration: 250
                easing.type: Easing.OutQuart
            }
        }
    }
    
    property Component cardHoverTransition: Component {
        ParallelAnimation {
            NumberAnimation {
                property: "scale"
                from: 1.0
                to: 1.02
                duration: 150
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "y"
                from: 0
                to: -2
                duration: 150
                easing.type: Easing.OutQuart
            }
        }
    }
    
    property Component buttonPressTransition: Component {
        SequentialAnimation {
            NumberAnimation {
                property: "scale"
                from: 1.0
                to: 0.98
                duration: 100
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "scale"
                from: 0.98
                to: 1.0
                duration: 100
                easing.type: Easing.OutQuart
            }
        }
    }
    
    // Loading animations
    property Component pulseAnimation: Component {
        SequentialAnimation {
            loops: Animation.Infinite
            
            NumberAnimation {
                property: "opacity"
                from: 1.0
                to: 0.5
                duration: 800
                easing.type: Easing.InOutQuad
            }
            NumberAnimation {
                property: "opacity"
                from: 0.5
                to: 1.0
                duration: 800
                easing.type: Easing.InOutQuad
            }
        }
    }
    
    property Component rotationAnimation: Component {
        RotationAnimation {
            from: 0
            to: 360
            duration: 1000
            loops: Animation.Infinite
            easing.type: Easing.Linear
        }
    }
    
    property Component breathingAnimation: Component {
        SequentialAnimation {
            loops: Animation.Infinite
            
            NumberAnimation {
                property: "scale"
                from: 1.0
                to: 1.05
                duration: 1500
                easing.type: Easing.InOutSine
            }
            NumberAnimation {
                property: "scale"
                from: 1.05
                to: 1.0
                duration: 1500
                easing.type: Easing.InOutSine
            }
        }
    }
    
    // Ripple effect animation
    property Component rippleAnimation: Component {
        ParallelAnimation {
            NumberAnimation {
                property: "scale"
                from: 0
                to: 1
                duration: 300
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "opacity"
                from: 0.5
                to: 0
                duration: 300
                easing.type: Easing.OutQuart
            }
        }
    }
    
    // Shake animation for errors
    property Component shakeAnimation: Component {
        SequentialAnimation {
            NumberAnimation {
                property: "x"
                from: 0
                to: 10
                duration: 100
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "x"
                from: 10
                to: -10
                duration: 100
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "x"
                from: -10
                to: 5
                duration: 100
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "x"
                from: 5
                to: -5
                duration: 100
                easing.type: Easing.OutQuart
            }
            NumberAnimation {
                property: "x"
                from: -5
                to: 0
                duration: 100
                easing.type: Easing.OutQuart
            }
        }
    }
    
    // Notification slide-in animation
    property Component notificationSlideIn: Component {
        ParallelAnimation {
            NumberAnimation {
                property: "x"
                from: 300
                to: 0
                duration: 400
                easing.type: Easing.OutBack
                easing.overshoot: 1.2
            }
            NumberAnimation {
                property: "opacity"
                from: 0
                to: 1
                duration: 300
                easing.type: Easing.OutQuart
            }
        }
    }
    
    // Smooth property change behaviors
    property Component smoothColorBehavior: Component {
        Behavior {
            ColorAnimation {
                duration: 200
                easing.type: Easing.OutQuart
            }
        }
    }
    
    property Component smoothNumberBehavior: Component {
        Behavior {
            NumberAnimation {
                duration: 200
                easing.type: Easing.OutQuart
            }
        }
    }
    
    property Component smoothOpacityBehavior: Component {
        Behavior {
            NumberAnimation {
                duration: 150
                easing.type: Easing.OutQuart
            }
        }
    }
    
    // Utility functions for animation management
    function playOnce(animation) {
        if (animation) {
            animation.restart()
        }
    }
    
    function stopAll(target) {
        if (target && target.children) {
            for (var i = 0; i < target.children.length; i++) {
                var child = target.children[i]
                if (child && typeof child.stop === "function") {
                    child.stop()
                }
            }
        }
    }
    
    function pauseAll(target) {
        if (target && target.children) {
            for (var i = 0; i < target.children.length; i++) {
                var child = target.children[i]
                if (child && typeof child.pause === "function") {
                    child.pause()
                }
            }
        }
    }
    
    function resumeAll(target) {
        if (target && target.children) {
            for (var i = 0; i < target.children.length; i++) {
                var child = target.children[i]
                if (child && typeof child.resume === "function") {
                    child.resume()
                }
            }
        }
    }
    
    // Animation state management
    property bool globalAnimationsEnabled: true
    
    function setGlobalAnimationsEnabled(enabled) {
        globalAnimationsEnabled = enabled
    }
    
    function createAnimationWithGlobalState(animationComponent, target, properties) {
        if (!globalAnimationsEnabled) {
            return null
        }
        
        return animationComponent.createObject(target, properties || {})
    }
}