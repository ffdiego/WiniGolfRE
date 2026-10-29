// Compares equal input grids; does not treat either simulator as the original game.
const fs=require('node:fs'),path=require('node:path');
const workspace=path.resolve(__dirname,'../..');
const a=JSON.parse(fs.readFileSync(path.join(workspace,'artifacts/simulator/sweep-reference-v1.json')));
const b=JSON.parse(fs.readFileSync(path.join(workspace,'artifacts/simulator/sweep-traversal-v2.json')));
if(a.errors.length||b.errors.length||a.results.length!==b.results.length)throw Error('Incomplete comparison');
const tolerance=1e-6;
const differences=a.results.map((x,i)=>{
 const y=b.results[i];
 if(x.courseId!==y.courseId||x.vx!==y.vx||x.vy!==y.vy||x.courseDataHash!==y.courseDataHash)throw Error('Input mismatch');
 const p=x.finalState.position,q=y.finalState.position;
 return {courseId:x.courseId,name:x.courseName,vx:x.vx,vy:x.vy,distance:Math.hypot(p.x-q.x,p.y-q.y,p.z-q.z),
  referenceSteps:x.steps,traversalSteps:y.steps,referenceReason:x.reason,traversalReason:y.reason,referenceOutsideFrames:x.outsideFrames};
});
const summary={shots:a.shots,referenceVersion:a.physicsVersion,traversalVersion:b.physicsVersion,
 referenceOutside:a.shotsWithOutsideFrames,traversalOutside:b.shotsWithOutsideFrames,
 referenceReasons:a.endReasons,traversalReasons:b.endReasons,finalPositionTolerance:tolerance,
 positionDifferencesBeyondTolerance:differences.filter(d=>d.distance>tolerance).length,
 stepDifferences:differences.filter(d=>d.referenceSteps!==d.traversalSteps).length,
 reasonDifferences:differences.filter(d=>d.referenceReason!==d.traversalReason).length,
 maxFinalDistance:Math.max(...differences.map(d=>d.distance)),
 largestDifferences:differences.filter(d=>d.distance>tolerance).sort((x,y)=>y.distance-x.distance).slice(0,10),differences};
fs.writeFileSync(path.join(workspace,'artifacts/simulator/comparison-v1-v2.json'),JSON.stringify(summary,null,2)+'\n');
console.log(JSON.stringify({...summary,differences:undefined,largestDifferences:undefined},null,2));
