cmake_minimum_required(VERSION 3.10)

project(PPMImage LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# The main entry point was renamed from ppm_image.cpp to main.cpp
add_executable(ppm_image main.cpp)