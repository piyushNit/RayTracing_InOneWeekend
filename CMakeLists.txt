cmake_minimum_required(VERSION 3.10)

project(PPMImage LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# The source file is named .h but contains main(), so we treat it as a source file
add_executable(ppm_image ppm_image.cpp)