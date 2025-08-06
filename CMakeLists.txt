cmake_minimum_required(VERSION 3.21)

project(Mystical VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

# Find Qt6 components first
find_package(Qt6 6.7 REQUIRED COMPONENTS 
    Core 
    Widgets 
    Quick 
    QuickControls2 
    Sql 
    Network 
    Concurrent
)

# Try to find optional components
find_package(Qt6 COMPONENTS WinExtras QUIET)
if(NOT Qt6WinExtras_FOUND)
    message(STATUS "Qt6WinExtras not found - Windows-specific features will be disabled")
endif()

# Enable Qt's standard project setup
qt_standard_project_setup()

# Now we can set Qt policies after loading Qt
qt_policy(SET QTP0001 NEW)
qt_policy(SET QTP0004 NEW)

# Define source files
set(SOURCES
    src/main.cpp
    src/core/GameInfo.cpp
    src/core/GameLibrary.cpp  
    src/core/GameLauncher.cpp
    src/core/Settings.cpp
    src/detection/PlatformDetector.cpp
    src/detection/SteamDetector.cpp
    src/detection/EpicDetector.cpp
    src/detection/GOGDetector.cpp
    src/detection/MinecraftDetector.cpp
    src/models/GameLibraryModel.cpp
    src/models/SettingsModel.cpp
    src/ui/MysticalStyle.cpp
    src/utils/FileUtils.cpp
    src/utils/RegistryUtils.cpp
    src/utils/ImageCache.cpp
)

set(HEADERS
    src/core/GameInfo.h
    src/core/GameLibrary.h
    src/core/GameLauncher.h
    src/core/Settings.h
    src/detection/PlatformDetector.h
    src/detection/SteamDetector.h
    src/detection/EpicDetector.h
    src/detection/GOGDetector.h
    src/detection/MinecraftDetector.h
    src/models/GameLibraryModel.h
    src/models/SettingsModel.h
    src/ui/MysticalStyle.h
    src/utils/FileUtils.h
    src/utils/RegistryUtils.h
    src/utils/ImageCache.h
)

# Create the executable
qt_add_executable(Mystical ${SOURCES} ${HEADERS})

# Add QML module (temporarily removed missing resources)
qt_add_qml_module(Mystical
    URI Mystical
    VERSION 1.0
    QML_FILES 
        qml/main.qml
        qml/components/GameCard.qml
        qml/components/GameGrid.qml
        qml/components/SearchBar.qml
        qml/components/SettingsPanel.qml
        qml/pages/LibraryPage.qml
        qml/pages/SettingsPage.qml
        qml/styles/Theme.qml
        qml/styles/Animations.qml
    RESOURCES
        resources/icons/mystical-icon.png
        resources/icons/steam-icon.png
        resources/icons/epic-icon.png  
        resources/icons/gog-icon.png
        resources/icons/minecraft-icon.png
        resources/icons/roblox-icon.png
        resources/images/default-cover.png
        resources/images/background-blur.png
)

# Link Qt libraries (using PRIVATE keyword)
target_link_libraries(Mystical PRIVATE
    Qt6::Core
    Qt6::Widgets  
    Qt6::Quick
    Qt6::QuickControls2
    Qt6::Sql
    Qt6::Network
    Qt6::Concurrent
)

# Windows-specific libraries for registry access
if(WIN32)
    target_link_libraries(Mystical PRIVATE advapi32)
endif()

# Set executable properties
set_target_properties(Mystical PROPERTIES
    WIN32_EXECUTABLE TRUE
    MACOSX_BUNDLE TRUE
)

# Enable Qt's automatic MOC, UIC, and RCC
set_target_properties(Mystical PROPERTIES
    AUTOMOC ON
    AUTORCC ON
    AUTOUIC ON
)

# Compiler-specific options
if(MSVC)
    target_compile_options(Mystical PRIVATE /W4)
else()
    target_compile_options(Mystical PRIVATE -Wall -Wextra -Wpedantic)
endif()

# Set output directories
set_target_properties(Mystical PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
)