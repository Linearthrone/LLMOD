// Neural Integration Art: Market Data and Somatic Feedback
// An abstract p5.js sketch representing the flow of sensory data through neural pathways
// focusing on cross-modal synthesis between market data and somatic feedback

let particles = [];
let connections = [];
let marketDataPoints = [];
let bodyPoints = [];
let noiseOffset = 0;

// Color palette
const DEEP_BURGUNDY = [139, 0, 0]; // #8B0000
const ELECTRIC_GOLD = [255, 215, 0]; // #FFD700
const NEURAL_PURPLE = [147, 112, 219]; // For neural pathways
const SOMATIC_BLUE = [65, 105, 225]; // For somatic feedback

function setup() {
  createCanvas(windowWidth, windowHeight);
  colorMode(RGB);
  noCursor();
  
  // Initialize particles for market data
  for (let i = 0; i < 100; i++) {
    particles.push({
      x: random(width),
      y: random(height),
      size: random(2, 8),
      speed: random(0.5, 2),
      direction: random(TWO_PI),
      type: 'market',
      age: 0,
      lifespan: random(200, 500),
      noiseOffset: random(1000)
    });
  }
  
  // Initialize particles for somatic feedback
  for (let i = 0; i < 50; i++) {
    particles.push({
      x: random(width),
      y: random(height),
      size: random(3, 10),
      speed: random(0.2, 1.5),
      direction: random(TWO_PI),
      type: 'somatic',
      age: 0,
      lifespan: random(300, 600),
      noiseOffset: random(1000)
    });
  }
  
  // Initialize connection pathways
  for (let i = 0; i < 30; i++) {
    connections.push({
      start: createVector(random(width), random(height)),
      end: createVector(random(width), random(height)),
      strength: random(0.1, 1),
      oscillation: random(TWO_PI)
    });
  }
  
  // Initialize market data points (lines/charts)
  for (let i = 0; i < 20; i++) {
    marketDataPoints.push({
      x: random(width),
      y: random(height),
      value: random(-100, 100),
      volatility: random(0.5, 2),
      frequency: random(0.01, 0.05)
    });
  }
  
  // Initialize body/somatic points
  for (let i = 0; i < 15; i++) {
    bodyPoints.push({
      x: random(width),
      y: random(height),
      pressure: random(0, 1),
      frequency: random(0.02, 0.1),
      amplitude: random(5, 20)
    });
  }
}

function draw() {
  // Deep space background with subtle gradient
  background(10, 5, 15, 20);
  
  // Update noise offset for fluid motion
  noiseOffset += 0.005;
  
  // Draw neural pathways
  drawNeuralPathways();
  
  // Draw market data visualization (lines, charts)
  drawMarketData();
  
  // Draw somatic feedback (body, touch)
  drawSomaticFeedback();
  
  // Update and draw particles
  updateAndDrawParticles();
  
  // Draw connection lines between elements
  drawConnections();
  
  // Draw title and info
  drawInfo();
}

function drawNeuralPathways() {
  // Draw flowing neural pathways with Perlin noise
  stroke(NEURAL_PURPLE[0], NEURAL_PURPLE[1], NEURAL_PURPLE[2], 50);
  strokeWeight(1);
  
  for (let i = 0; i < 20; i++) {
    let xoff = noiseOffset + i * 0.1;
    let yoff = noiseOffset * 1.3 + i * 0.2;
    
    beginShape();
    for (let j = 0; j < 100; j++) {
      let x = map(noise(xoff), 0, 1, 0, width);
      let y = map(noise(yoff), 0, 1, 0, height);
      vertex(x, y);
      xoff += 0.05;
      yoff += 0.03;
    }
    endShape();
  }
}

function drawMarketData() {
  // Draw market data as abstract lines and charts
  noFill();
  
  // Draw price movement lines with Electric Gold
  stroke(ELECTRIC_GOLD[0], ELECTRIC_GOLD[1], ELECTRIC_GOLD[2], 150);
  strokeWeight(2);
  
  for (let i = 0; i < marketDataPoints.length; i++) {
    let point = marketDataPoints[i];
    let y = point.y + sin(frameCount * point.frequency + point.x * 0.01) * point.volatility * 10;
    
    // Draw connected lines
    if (i > 0) {
      let prevPoint = marketDataPoints[i-1];
      let prevY = prevPoint.y + sin(frameCount * prevPoint.frequency + prevPoint.x * 0.01) * prevPoint.volatility * 10;
      line(prevPoint.x, prevY, point.x, y);
    }
    
    // Draw data points
    strokeWeight(4);
    point(point.x, y);
    strokeWeight(2);
  }
  
  // Draw abstract candlestick-like elements
  for (let i = 0; i < 10; i++) {
    let x = (width / 10) * i + width / 20;
    let open = height/2 + sin(frameCount * 0.02 + i) * 50;
    let close = height/2 + sin(frameCount * 0.02 + i + 0.5) * 50;
    let high = min(open, close) - random(10, 30);
    let low = max(open, close) + random(10, 30);
    
    // Wick
    stroke(ELECTRIC_GOLD[0], ELECTRIC_GOLD[1], ELECTRIC_GOLD[2], 100);
    line(x, high, x, low);
    
    // Body
    let bodyHeight = abs(close - open);
    if (bodyHeight < 2) bodyHeight = 2;
    
    if (close > open) {
      fill(DEEP_BURGUNDY[0], DEEP_BURGUNDY[1], DEEP_BURGUNDY[2], 150);
      stroke(ELECTRIC_GOLD[0], ELECTRIC_GOLD[1], ELECTRIC_GOLD[2], 200);
    } else {
      noFill();
      stroke(DEEP_BURGUNDY[0], DEEP_BURGUNDY[1], DEEP_BURGUNDY[2], 200);
    }
    
    rect(x - 3, min(open, close), 6, bodyHeight);
  }
}

function drawSomaticFeedback() {
  // Draw somatic feedback as body/touch representations
  noStroke();
  
  // Draw abstract body forms
  for (let i = 0; i < bodyPoints.length; i++) {
    let point = bodyPoints[i];
    let pulse = sin(frameCount * point.frequency) * point.amplitude * point.pressure;
    
    // Draw pressure sensitive points
    fill(SOMATIC_BLUE[0], SOMATIC_BLUE[1], SOMATIC_BLUE[2], 100 + point.pressure * 100);
    ellipse(point.x, point.y, 10 + pulse, 10 + pulse);
    
    // Draw connection lines between nearby points
    for (let j = i + 1; j < bodyPoints.length; j++) {
      let otherPoint = bodyPoints[j];
      let d = dist(point.x, point.y, otherPoint.x, otherPoint.y);
      
      if (d < 150) {
        let alpha = map(d, 0, 150, 50, 0);
        stroke(SOMATIC_BLUE[0], SOMATIC_BLUE[1], SOMATIC_BLUE[2], alpha);
        line(point.x, point.y, otherPoint.x, otherPoint.y);
        noStroke();
      }
    }
  }
  
  // Draw touch sensitivity visualization
  fill(SOMATIC_BLUE[0], SOMATIC_BLUE[1], SOMATIC_BLUE[2], 30);
  let touchX = noise(noiseOffset * 0.5) * width;
  let touchY = noise(noiseOffset * 0.7) * height;
  let touchSize = 30 + sin(frameCount * 0.1) * 10;
  ellipse(touchX, touchY, touchSize, touchSize);
}

function updateAndDrawParticles() {
  // Update and draw particles with fluid motion
  for (let i = particles.length - 1; i >= 0; i--) {
    let p = particles[i];
    
    // Update particle age
    p.age++;
    
    // Remove dead particles
    if (p.age > p.lifespan) {
      particles.splice(i, 1);
      continue;
    }
    
    // Apply Perlin noise for fluid motion
    let noiseX = noise(p.noiseOffset, noiseOffset);
    let noiseY = noise(p.noiseOffset + 1000, noiseOffset);
    p.direction += map(noiseX, 0, 1, -0.1, 0.1);
    
    // Update position
    p.x += cos(p.direction) * p.speed;
    p.y += sin(p.direction) * p.speed;
    
    // Boundary check with wrapping
    if (p.x < 0) p.x = width;
    if (p.x > width) p.x = 0;
    if (p.y < 0) p.y = height;
    if (p.y > height) p.y = 0;
    
    // Draw particle based on type
    let alpha = map(p.age, 0, p.lifespan, 200, 0);
    
    if (p.type === 'market') {
      fill(ELECTRIC_GOLD[0], ELECTRIC_GOLD[1], ELECTRIC_GOLD[2], alpha);
    } else if (p.type === 'somatic') {
      fill(SOMATIC_BLUE[0], SOMATIC_BLUE[1], SOMATIC_BLUE[2], alpha);
    }
    
    noStroke();
    ellipse(p.x, p.y, p.size, p.size);
    
    // Add new particles occasionally
    if (frameCount % 30 === 0 && random() < 0.3) {
      particles.push({
        x: random(width),
        y: random(height),
        size: random(2, 8),
        speed: random(0.5, 2),
        direction: random(TWO_PI),
        type: random() > 0.5 ? 'market' : 'somatic',
        age: 0,
        lifespan: random(200, 500),
        noiseOffset: random(1000)
      });
    }
  }
}

function drawConnections() {
  // Draw connection lines between different elements
  stroke(NEURAL_PURPLE[0], NEURAL_PURPLE[1], NEURAL_PURPLE[2], 30);
  strokeWeight(0.5);
  
  // Connect market data points to somatic points
  for (let i = 0; i < marketDataPoints.length; i++) {
    for (let j = 0; j < bodyPoints.length; j++) {
      let d = dist(marketDataPoints[i].x, marketDataPoints[i].y, bodyPoints[j].x, bodyPoints[j].y);
      if (d < 200) {
        let alpha = map(d, 0, 200, 50, 0);
        stroke(NEURAL_PURPLE[0], NEURAL_PURPLE[1], NEURAL_PURPLE[2], alpha);
        line(marketDataPoints[i].x, marketDataPoints[i].y, bodyPoints[j].x, bodyPoints[j].y);
      }
    }
  }
  
  noStroke();
}

function drawInfo() {
  // Draw title and information
  fill(ELECTRIC_GOLD[0], ELECTRIC_GOLD[1], ELECTRIC_GOLD[2]);
  textSize(20);
  textAlign(CENTER, TOP);
  text("Neural Integration: Market Data & Somatic Feedback", width/2, 20);
  
  textSize(14);
  text("Cross-modal synthesis visualization using Deep Burgundy and Electric Gold", width/2, 50);
  
  // Draw legend
  textSize(12);
  textAlign(LEFT, TOP);
  fill(DEEP_BURGUNDY[0], DEEP_BURGUNDY[1], DEEP_BURGUNDY[2]);
  text("Market Data (Lines, Charts)", 20, height - 80);
  
  fill(SOMATIC_BLUE[0], SOMATIC_BLUE[1], SOMATIC_BLUE[2]);
  text("Somatic Feedback (Body, Touch)", 20, height - 60);
  
  fill(NEURAL_PURPLE[0], NEURAL_PURPLE[1], NEURAL_PURPLE[2]);
  text("Neural Pathways (Integration)", 20, height - 40);
  
  fill(ELECTRIC_GOLD[0], ELECTRIC_GOLD[1], ELECTRIC_GOLD[2]);
  text("Particle Systems (Flow)", 20, height - 20);
}

function windowResized() {
  resizeCanvas(windowWidth, windowHeight);
}