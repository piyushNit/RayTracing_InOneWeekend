#include "classes/rtweekend.h"

#include "classes/camera.h"
#include "classes/hittable.h"
#include "classes/hittable_list.h"
#include "classes/material.h"
#include "classes/sphere.h"


color lerp_color(color colA, color colB, double t) {
    return (1.0-t)*colA + t*colB;
}

color ray_color(const ray& r, const hittable& world) {
    hit_record rec;
    if (world.hit(r, interval(0, infinity), rec)) {
        return 0.5 * (rec.normal + color(1,1,1));
    }

    vec3 unit_direction = unit_vector(r.direction());
    auto a = 0.5*(unit_direction.y() + 1.0);
    return lerp_color(color(1.0, 1.0, 1.0), color(0.5, 0.7, 1.0), a);
}

int main() {
    hittable_list world;

    auto material_ground = make_shared<lambertian>(color(0.8, 0.8, 0.0));
    auto material_center = make_shared<lambertian>(color(0.1, 0.2, 0.5));
    //auto material_left   = make_shared<metal>(color(0.8, 0.8, 0.8), 0.3);
    auto material_left   = make_shared<dielectric>(1.50);
    auto material_bubble   = make_shared<dielectric>(1.00 / 1.33);
    auto material_right  = make_shared<metal>(color(0.8, 0.6, 0.2), 1.0);


    world.add(make_shared<sphere>(point3( 0.0, -100.5, -1.0), 100.0, material_ground));
    world.add(make_shared<sphere>(point3( 0.0,    0.0, -1.2),   0.5, material_center));
    world.add(make_shared<sphere>(point3(-1.0,    0.0, -1.0),   0.5, material_left));
    world.add(make_shared<sphere>(point3(-1.0,    0.0, -1.0),   0.4, material_bubble));
    world.add(make_shared<sphere>(point3( 1.0,    0.0, -1.0),   0.5, material_right));

    camera cam;

    cam.aspect_ratio = 16.0 / 9.0;
    cam.image_width  = 400;
    cam.samples_per_pixel = 100;
    cam.max_depth         = 50;

    cam.render(world);
}