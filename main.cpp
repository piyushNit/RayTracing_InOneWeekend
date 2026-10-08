#include "classes/rtweekend.h"

#include "classes/camera.h"
#include "classes/hittable.h"
#include "classes/hittable_list.h"
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

    world.add(make_shared<sphere>(point3(0,0,-1), 0.5));
    world.add(make_shared<sphere>(point3(0,-100.5,-1), 100));

    camera cam;

    cam.aspect_ratio = 16.0 / 9.0;
    cam.image_width  = 400;

    cam.render(world);
}