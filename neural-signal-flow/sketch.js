let particles = [];
let connections = [];
let bgColor;
let burgundy, gold;
let flowField = [];
let cols, rows;
let resolution = 10;

function setup() {
  createCanvas(windowWidth, windowHeight);
  
  // Set up colors
  bgColor = color(10, 5, 20);
  burgundy = color(139, 0, 0); // #8B0000
  gold = color(255, 215, 0);   // #FFD700
  
  // Initialize flow field
  cols = floor(width / resolution);
  rows = floor(height / resolution);
  flowField = new Array(cols * rows);
  
  // Create initial particles
  for (let i = 0; i < 150; i++) {
    particles.push(new Particle());
  }
  
  // Create connection points for feedback loops
  for (let i = 0; i < 8; i++) {
    connections.push({
      x: random(width),
      y: random(height),
      radius: random(30, 80),
      strength: random(-0.5, 0.5),
      pulse: random(TWO_PI)
    });
  }
  
  // Set up additive blending
  blendMode(ADD);
}

function draw() {
  background(bgColor);
  
  // Update flow field with Perlin noise
  let yoff = 0;
  for (let y = 0; y < rows; y++) {
    let xoff = 0;
    for (let x = 0; x < cols; x++) {
      let angle = noise(xoff, yoff, frameCount * 0.005) * TWO_PI * 2;
      let index = x + y * cols;
      flowField[index] = p5.Vector.fromAngle(angle);
      xoff += 0.1;
    }
    yoff += 0.1;
  }
  
  // Draw connection points (neural nodes)
  for (let i = 0; i < connections.length; i++) {
    let c = connections[i];
    c.pulse += 0.05;
    
    // Pulsing effect
    let pulseRadius = c.radius + sin(c.pulse) * 10;
    
    // Draw connection point
    noStroke();
    fill(red(gold), green(gold), blue(gold), 100);
    ellipse(c.x, c.y, pulseRadius * 2);
    
    // Draw inner core
    fill(red(burgundy), green(burgundy), blue(burgundy), 200);
    ellipse(c.x, c.y, pulseRadius);
  }
  
  // Update and display particles
  for (let i = 0; i < particles.length; i++) {
    let p = particles[i];
    
    // Apply flow field force
    let x = floor(p.pos.x / resolution);
    let y = floor(p.pos.y / resolution);
    let index = x + y * cols;
    if (index >= 0 && index < flowField.length) {
      p.applyForce(flowField[index]);
    }
    
    // Apply connection forces
    for (let j = 0; j < connections.length; j++) {
      let c = connections[j];
      let force = p5.Vector.sub(createVector(c.x, c.y), p.pos);
      let distance = force.mag();
      
      if (distance < c.radius * 2) {
        force.normalize();
        force.mult(c.strength);
        p.applyForce(force);
      }
    }
    
    p.update();
    p.display();
    p.edges();
  }
  
  // Draw connections between nearby particles (synaptic connections)
  strokeWeight(0.5);
  for (let i = 0; i < particles.length; i++) {
    for (let j = i + 1; j < particles.length; j++) {
      let p1 = particles[i];
      let p2 = particles[j];
      let distance = dist(p1.pos.x, p1.pos.y, p2.pos.x, p2.pos.y);
      
      if (distance < 100) {
        let alpha = map(distance, 0, 100, 100, 0);
        stroke(red(gold), green(gold), blue(gold), alpha);
        line(p1.pos.x, p1.pos.y, p2.pos.x, p2.pos.y);
      }
    }
  }
}

function windowResized() {
  resizeCanvas(windowWidth, windowHeight);
  cols = floor(width / resolution);
  rows = floor(height / resolution);
  flowField = new Array(cols * rows);
}

function mousePressed() {
  // Add new connection point at mouse position
  connections.push({
    x: mouseX,
    y: mouseY,
    radius: random(30, 80),
    strength: random(-0.5, 0.5),
    pulse: random(TWO_PI)
  });
  
  // Add more particles
  for (let i = 0; i < 10; i++) {
    particles.push(new Particle(mouseX, mouseY));
  }
}

function mouseDragged() {
  // Add particles while dragging
  particles.push(new Particle(mouseX, mouseY));
  
  // Limit particles to prevent performance issues
  if (particles.length > 300) {
    particles.shift();
  }
}

class Particle {
  constructor(x, y) {
    this.pos = createVector(x || random(width), y || random(height));
    this.vel = createVector(0, 0);
    this.acc = createVector(0, 0);
    this.maxspeed = 4;
    this.size = random(2, 6);
    
    // Color properties
    this.colorRatio = random(1);
    this.color = lerpColor(burgundy, gold, this.colorRatio);
  }
  
  applyForce(force) {
    this.acc.add(force);
  }
  
  update() {
    this.vel.add(this.acc);
    this.vel.limit(this.maxspeed);
    this.pos.add(this.vel);
    this.acc.mult(0);
  }
  
  display() {
    noStroke();
    fill(this.color);
    ellipse(this.pos.x, this.pos.y, this.size);
  }
  
  edges() {
    if (this.pos.x > width) this.pos.x = 0;
    else if (this.pos.x < 0) this.pos.x = width;
    if (this.pos.y > height) this.pos.y = 0;
    else if (this.pos.y < 0) this.pos.y = height;
  }
}