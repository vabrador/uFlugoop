uFlugoop: Particle-based Fluid Simulation on the GPU for Unity
===

Origin
---

uFlex (NVidia FleX for Unity) doesn't support OS X or non-nVidia cards. :(

So to satisfy my _particular_ desires this holiday, I spun a simple GPU fluid simulation. This is very, very far from FleX or uFlex, but hey, it's sure is some fluids!

Based on Hegeman, Carr, and Miller's _Particle-based Fluid Simulation on the GPU_: 
http://ldc.usb.ve/~vtheok/cursos/ci6323/pdf/lecturas/Particle-Based%20Fluid%20Simulation%20on%20the%20GPU%20.pdf


The Particle Quadtree is a 2D Texture with Special Mipmapping
---

One two-dimensional float4 array (texture) holds spherical particles where RGBA is mapped to XYZR (R for radius). Two other 2D int3 arrays of the same size as the first texture are used to store links to other indices in the first texture, allowing traversal of the quadtree. Confused? Particles and links are stored thusly:

* particleTex : float4[NxN]
	* RGBA -> "XYZR"; Spherical position + radius
	* In an ideal scenario, particles are spatially partitioned within the texture, but this is an optimization rather than a requirement. The quadtree is rebuilt every time particle positions change, but with the original tree topology. (In the next section I discuss optimization further, which _does_ change the topology of the tree.) To determine a parent node from four child nodes (a 2x2 texel square), define a sphere (XYZR) that contains each of its four child spheres; this step, performed recursively, produces mipmaps, where each mip level represents a higher, larger-spherical-volume level of the quadtree.
* hitLinkTex : int3[NxN]
	* XYZ : "XYM"; Coordinate + mip level in particleTex
	* Aligned with particleTex per mip level; each texel corresponds to a coordinate + mip to the next texel that should be tested, to be used if collision is detected for the particleTex at the current coordinate. Hit Links either link into a more detailed mip level (if not a leaf) or have the sentinel value M = -1 to indicate having reached a leaf, at which point application-specific collision logic can be applied using the resulting particle sphere that has been collided.
* missLinkTex : int3[NxN]
	* XYZ : "XYM"; Coordinate + mip level in particleTex
	* Aligned with particleTex per mip level; as hitLinkTex, but is to be followed when there is no collision. Miss Links either link to sibling nodes or higher mip levels, or the sentinel value (-1, -1, -1) to indicate having reached the end of progression for this path.
	* Both hitLinkTex and missLinkTex can be computed as soon as the particleTex quadtree is organized.


Optimize the Quadtree Infrequently on the CPU
---

The requirement for our quadtree is merely that each higher mip level describes an XYZR sphere that contains each of its four child spheres at the next mip level down. So the "construction" of the quadtree is merely the construction of the particle texture's mipmaps and the appropriate hit/miss links that enable tree traversal. However, if this quadtree-mipmapping is constructed for randomly-placed particles, the spheres at higher mip levels will tend to be larger than they have to be, resulting in more "potential hits" found per query, which will result in worse performance.

For best performance, we want to reorganize the particles in the particle texture such that particles that are near one another in 3-space are also adjacent or near one another in the particle texture.


Query the Quadtree on the GPU As Desired
---

Querying for particle collisions begins at the highest mip level and proceeds deeper into the quadtree.

To perform logic based on all colliding particles, given an input float4 querySphere:

* Initialize position as an int3: XYMIdx = int3(0, 0, M). M is the highest mip level available; M = log_2(N). N = particleTex size along one dimension.
* Initialize nextPosition as position.
* While nextPosition.M is not -1:
	* Check collision against mip level M: Collide particleTex_mipM (XYZR) against querySphere (XYZR).
	* If the spheres collide:
		* Set nextPosition as hitLinkTex[XYMIdx].
		* If the nextPosition.M is -1:
			* Do application-specific collision logic for the particle it particleTex_mip0[XYMIdx].
	* Set nextPosition as missLinkTex[XYMIdx].

Querying for particles in this way is O(log(N)), as long as the quadtree remains optimized. Thankfully, even in a dynamic particle system, the quality of the quadtree degrades slowly over time, so it only requires infrequent optimizations. (About every second if the optimization is good.)

Raymarching Renderer
---





stuff








