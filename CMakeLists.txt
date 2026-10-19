cmake_minimum_required(VERSION 3.15)

project(RayTracingInOneWeekend LANGUAGES CXX)

# C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Put the built executable next to the build directory (e.g. build/ppm_image)
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR})

# Compiler warnings
if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang|AppleClang")
    add_compile_options(-Wall -Wextra -Wpedantic)
elseif(MSVC)
    add_compile_options(/W4)
endif()

# The headers are included with a "classes/" prefix (e.g. #include "classes/rtweekend.h")
# from main.cpp, and headers include each other by plain name (e.g. #include "color.h"),
# both of which resolve relative to the including file's directory, so no extra
# include directories are required.

# Main entry point (renamed from ppm_image.cpp to main.cpp)
add_executable(ppm_image main.cpp)
